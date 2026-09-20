using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jev.Codex;

public sealed class CodexCliClient(
    IOptions<CodexCliOptions> optionsAccessor,
    ICodexProcessRunner processRunner,
    ILogger<CodexCliClient> logger) : ICodexCli
{
    private readonly CodexCliOptions _options = optionsAccessor.Value;
    private readonly CodexCommandBuilder _commands = new(optionsAccessor.Value);

    public async Task<CodexAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var displayPath = CliExecutableResolver.TryResolve(_options.ExecutablePath)?.Path ?? _options.ExecutablePath;
            var versionInfo = CreateStartInfo(_options.ExecutablePath, _commands.BuildVersionArguments(), Directory.GetCurrentDirectory());
            var versionRun = await processRunner.RunAsync(versionInfo, standardInput: null, stdoutLine: null, stderrLine: null, cancellationToken);
            var version = (versionRun.Stdout + " " + versionRun.Stderr).Trim().ReplaceLineEndings(" ").Trim();
            if (versionRun.ExitCode != 0)
            {
                return new CodexAvailability
                {
                    IsInstalled = true,
                    IsLoggedIn = null,
                    ExecutablePath = displayPath,
                    Error = $"'{displayPath} --version' exited {versionRun.ExitCode}."
                };
            }

            var loginInfo = CreateStartInfo(_options.ExecutablePath, _commands.BuildLoginStatusArguments(), Directory.GetCurrentDirectory());
            var loginRun = await processRunner.RunAsync(loginInfo, standardInput: null, stdoutLine: null, stderrLine: null, cancellationToken);
            var loggedIn = loginRun.ExitCode == 0;
            return new CodexAvailability
            {
                IsInstalled = true,
                IsLoggedIn = loggedIn,
                Version = string.IsNullOrWhiteSpace(version) ? null : version,
                ExecutablePath = displayPath,
                Error = loggedIn
                    ? null
                    : $"Run `{CodexAvailability.LoginCommandText}` once with a ChatGPT subscription. Do not set OPENAI_API_KEY for Codex."
            };
        }
        catch (CodexNotInstalledException ex)
        {
            return new CodexAvailability
            {
                IsInstalled = false,
                IsLoggedIn = false,
                ExecutablePath = _options.ExecutablePath,
                Error = ex.Message
            };
        }
    }

    public async Task<CodexExecResult> ExecAsync(
        CodexExecRequest request,
        IProgress<CodexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

        var workingDirectory = await EnsureWorkspaceAsync(request, cancellationToken);
        var arguments = _commands.BuildExecArguments(request, workingDirectory);
        var commandLine = CodexCommandBuilder.FormatCommandLine(_options.ExecutablePath, arguments);
        var events = new List<CodexJsonEvent>();

        progress?.Report(new CodexProgress("starting", $"Starting Codex in {workingDirectory}"));
        progress?.Report(new CodexProgress("input", commandLine));
        progress?.Report(new CodexProgress("input", "stdin: " + request.Prompt));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        ProcessRunResult run;
        try
        {
            var startInfo = CreateStartInfo(_options.ExecutablePath, arguments, workingDirectory);
            run = await processRunner.RunAsync(
                startInfo,
                request.Prompt,
                new ImmediateProgress<string>(line =>
                {
                    progress?.Report(new CodexProgress("stdout", line));
                    var parsed = CodexJsonEventParser.TryParse(line);
                    if (parsed is null)
                    {
                        return;
                    }

                    lock (events)
                    {
                        events.Add(parsed);
                    }

                    progress?.Report(ToProgress(parsed));
                }),
                new ImmediateProgress<string>(line => progress?.Report(new CodexProgress("stderr", line))),
                timeout.Token);
        }
        catch (CodexNotInstalledException ex)
        {
            logger.LogWarning(ex, "Codex CLI is not installed.");
            return new CodexExecResult
            {
                Succeeded = false,
                ExitCode = 127,
                Error = ex.Message,
                CommandLine = commandLine,
                WorkingDirectory = workingDirectory
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CodexExecResult
            {
                Succeeded = false,
                ExitCode = 124,
                Error = $"Codex timed out after {_options.TimeoutSeconds} seconds.",
                CommandLine = commandLine,
                WorkingDirectory = workingDirectory,
                Events = events
            };
        }

        // Rebuild from captured stdout so final fields do not depend on IProgress timing.
        List<CodexJsonEvent> snapshot;
        lock (events)
        {
            events.Clear();
            foreach (var line in SplitLines(run.Stdout))
            {
                var parsed = CodexJsonEventParser.TryParse(line);
                if (parsed is not null)
                {
                    events.Add(parsed);
                }
            }

            snapshot = [.. events];
        }

        var parsedError = CodexJsonEventParser.FirstError(snapshot);
        var finalMessage = CodexJsonEventParser.LastAgentMessage(snapshot)
            ?? (snapshot.Count == 0 ? run.Stdout.Trim() : null);
        var succeeded = run.ExitCode == 0 && parsedError is null;
        var combined = $"{parsedError}{Environment.NewLine}{run.Stderr}{Environment.NewLine}{finalMessage}";
        var error = succeeded
            ? null
            : LooksLikeAuthFailure(combined)
                ? $"Codex is not signed in. Run `{CodexAvailability.LoginCommandText}` once with a ChatGPT subscription. Do not set OPENAI_API_KEY for Codex."
                : parsedError ?? $"codex exited {run.ExitCode}.";

        return new CodexExecResult
        {
            Succeeded = succeeded,
            ExitCode = run.ExitCode,
            ThreadId = CodexJsonEventParser.FirstThreadId(snapshot),
            FinalMessage = string.IsNullOrWhiteSpace(finalMessage) ? null : finalMessage,
            Error = error,
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            ChangedFiles = CodexJsonEventParser.DistinctChangedFiles(snapshot),
            CommandsRun = CodexJsonEventParser.CommandsRun(snapshot),
            Stderr = run.Stderr.Trim(),
            Events = snapshot
        };
    }

    private async Task<string> EnsureWorkspaceAsync(CodexExecRequest request, CancellationToken cancellationToken)
    {
        var path = request.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = string.IsNullOrWhiteSpace(_options.DefaultWorkspaceRoot)
                ? Path.Combine(Path.GetTempPath(), "jev-workspaces", Guid.NewGuid().ToString("n")[..8])
                : _options.DefaultWorkspaceRoot;
        }

        path = Path.GetFullPath(path);
        if (request.CreateWorkspaceIfMissing && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Codex workspace '{path}' does not exist.");
        }

        await Task.CompletedTask.WaitAsync(cancellationToken);
        return path;
    }

    private static ProcessStartInfo CreateStartInfo(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static bool LooksLikeAuthFailure(string? text)
        => !string.IsNullOrWhiteSpace(text)
           && (text.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not signed in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please log in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please login", StringComparison.OrdinalIgnoreCase)
               || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase)
               || text.Contains("authentication required", StringComparison.OrdinalIgnoreCase));

    private static CodexProgress ToProgress(CodexJsonEvent parsed)
    {
        var message = parsed.Type switch
        {
            "thread.started" => $"Codex thread {parsed.ThreadId}",
            "turn.started" => "Codex is working",
            "turn.completed" => "Codex turn completed",
            "turn.failed" => parsed.Error ?? "Codex turn failed",
            "error" => parsed.Error ?? "Codex error",
            _ when parsed.Command is not null => $"Codex command: {parsed.Command}",
            _ when parsed.IsAgentMessage => "Codex wrote a reply",
            _ when parsed.ChangedPaths.Count > 0 => $"Codex changed {string.Join(", ", parsed.ChangedPaths)}",
            _ => parsed.Text ?? parsed.Type
        };

        return new CodexProgress(parsed.Type, message, parsed);
    }

    private static IEnumerable<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        foreach (var line in normalized.Split('\n'))
        {
            yield return line;
        }
    }
}
