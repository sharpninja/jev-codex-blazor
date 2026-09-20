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
        var startInfo = CreateStartInfo(_options.ExecutablePath, _commands.BuildVersionArguments(), Directory.GetCurrentDirectory());

        try
        {
            var run = await processRunner.RunAsync(startInfo, standardInput: null, stdoutLine: null, cancellationToken);
            var version = (run.Stdout + " " + run.Stderr).Trim();
            var installed = run.ExitCode == 0;
            return new CodexAvailability
            {
                IsInstalled = installed,
                Version = string.IsNullOrWhiteSpace(version) ? null : version.ReplaceLineEndings(" ").Trim(),
                ExecutablePath = _options.ExecutablePath,
                Error = installed ? null : $"'{_options.ExecutablePath} --version' exited {run.ExitCode}."
            };
        }
        catch (CodexNotInstalledException ex)
        {
            return new CodexAvailability
            {
                IsInstalled = false,
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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        ProcessRunResult run;
        try
        {
            var startInfo = CreateStartInfo(_options.ExecutablePath, arguments, workingDirectory);
            run = await processRunner.RunAsync(
                startInfo,
                request.Prompt,
                new Progress<string>(line =>
                {
                    var parsed = CodexJsonEventParser.TryParse(line);
                    if (parsed is null)
                    {
                        return;
                    }

                    events.Add(parsed);
                    progress?.Report(ToProgress(parsed));
                }),
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

        // Plain-text fallback: if --json was unavailable, keep the last stdout block.
        if (events.Count == 0 && !string.IsNullOrWhiteSpace(run.Stdout))
        {
            foreach (var line in run.Stdout.Split('\n'))
            {
                var parsed = CodexJsonEventParser.TryParse(line);
                if (parsed is not null)
                {
                    events.Add(parsed);
                }
            }
        }

        var parsedError = CodexJsonEventParser.FirstError(events);
        var finalMessage = CodexJsonEventParser.LastAgentMessage(events)
            ?? (events.Count == 0 ? run.Stdout.Trim() : null);
        var succeeded = run.ExitCode == 0 && parsedError is null;

        return new CodexExecResult
        {
            Succeeded = succeeded,
            ExitCode = run.ExitCode,
            ThreadId = CodexJsonEventParser.FirstThreadId(events),
            FinalMessage = string.IsNullOrWhiteSpace(finalMessage) ? null : finalMessage,
            Error = parsedError ?? (succeeded ? null : $"codex exited {run.ExitCode}."),
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            ChangedFiles = CodexJsonEventParser.DistinctChangedFiles(events),
            CommandsRun = CodexJsonEventParser.CommandsRun(events),
            Stderr = run.Stderr.Trim(),
            Events = events
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
}
