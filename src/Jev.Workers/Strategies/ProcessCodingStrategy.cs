using System.Diagnostics;
using Jev.Codex;
using Jev.Workers.Process;
using Microsoft.Extensions.Logging;

namespace Jev.Workers.Strategies;

public abstract class ProcessCodingStrategy(
    ICliProcessRunner processRunner,
    ILogger logger,
    ICodingHost? host = null) : ICodingAgentStrategy
{
    private readonly ICodingHost _host = host ?? CodingHost.Desktop("desktop");

    public abstract CodingStrategyKind Kind { get; }

    public abstract string DisplayName { get; }

    public abstract string ExecutablePath { get; }

    public string LoginCommand => SubscriptionAuth.LoginCommand(Kind);

    protected abstract int TimeoutSeconds { get; }

    protected abstract string? DefaultWorkspaceRoot { get; }

    protected abstract IReadOnlyList<string> BuildVersionArguments();

    protected virtual IReadOnlyList<IReadOnlyList<string>> BuildVersionArgumentCandidates()
        => [BuildVersionArguments()];

    protected abstract IReadOnlyList<string>? BuildAuthStatusArguments();

    protected abstract IReadOnlyList<string> BuildExecArguments(CodingTaskRequest request, string workingDirectory);

    protected virtual string? StandardInput(CodingTaskRequest request) => null;

    public async Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!_host.SupportsLocalCli)
        {
            return Unsupported();
        }

        try
        {
            var displayPath = ResolvedDisplayPath();
            CliProcessRunResult? versionRun = null;
            IReadOnlyList<string> versionArgs = [];
            foreach (var candidate in BuildVersionArgumentCandidates())
            {
                var versionInfo = CreateStartInfo(ExecutablePath, candidate, Directory.GetCurrentDirectory());
                versionRun = await processRunner.RunAsync(versionInfo, null, null, null, cancellationToken);
                versionArgs = candidate;
                if (versionRun.ExitCode == 0)
                {
                    break;
                }
            }

            var version = versionRun is null
                ? ""
                : (versionRun.Stdout + " " + versionRun.Stderr).Trim().ReplaceLineEndings(" ");
            if (versionRun is null || versionRun.ExitCode != 0)
            {
                var invoked = string.Join(' ', versionArgs);
                return InstalledUnhealthy(
                    displayPath,
                    $"'{displayPath} {invoked}' exited {versionRun?.ExitCode ?? -1}.");
            }

            var authArgs = BuildAuthStatusArguments();
            if (authArgs is null)
            {
                return new CodingAvailability
                {
                    Kind = Kind,
                    IsInstalled = true,
                    IsLoggedIn = null,
                    Version = string.IsNullOrWhiteSpace(version) ? null : version,
                    ExecutablePath = displayPath,
                    LoginCommand = this.LoginCommand
                };
            }

            var authInfo = CreateStartInfo(ExecutablePath, authArgs, Directory.GetCurrentDirectory());
            var authRun = await processRunner.RunAsync(authInfo, null, null, null, cancellationToken);
            var loggedIn = authRun.ExitCode == 0;
            return new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = true,
                IsLoggedIn = loggedIn,
                Version = string.IsNullOrWhiteSpace(version) ? null : version,
                ExecutablePath = displayPath,
                LoginCommand = this.LoginCommand,
                Error = loggedIn ? null : SubscriptionAuth.NotLoggedInMessage(Kind, DisplayName)
            };
        }
        catch (CliExecutableNotFoundException ex)
        {
            return Missing(ex.Message);
        }
        catch (CodingWorkerNotInstalledException ex)
        {
            return Missing(ex.Message);
        }
    }

    public async Task<CodingTaskResult> RunAsync(
        CodingTaskRequest request,
        IProgress<CodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        if (!_host.SupportsLocalCli)
        {
            var unsupported = CodingHost.UnsupportedMessage(_host);
            progress?.Report(new CodingProgress(CodingProgress.System, unsupported));
            return Failed(126, "", "", unsupported);
        }

        var workingDirectory = WorkspacePath.Ensure(request.WorkingDirectory, DefaultWorkspaceRoot, request.CreateWorkspaceIfMissing);
        var arguments = BuildExecArguments(request, workingDirectory);
        var commandLine = CliCommandLine.Format(ResolvedDisplayPath(), arguments);
        var stdin = StandardInput(request);
        progress?.Report(new CodingProgress(CodingProgress.Starting, $"Starting {DisplayName} in {workingDirectory}"));
        progress?.Report(new CodingProgress(CodingProgress.Input, SecretSanitizer.Redact(commandLine)));
        if (!string.IsNullOrEmpty(stdin))
        {
            progress?.Report(new CodingProgress(CodingProgress.Input, "stdin: " + SecretSanitizer.Redact(stdin)));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds)));

        CliProcessRunResult run;
        try
        {
            var startInfo = CreateStartInfo(ExecutablePath, arguments, workingDirectory);
            run = await processRunner.RunAsync(
                startInfo,
                stdin,
                new ImmediateProgress<string>(line => progress?.Report(new CodingProgress(CodingProgress.Stdout, SecretSanitizer.Redact(line)))),
                new ImmediateProgress<string>(line => progress?.Report(new CodingProgress(CodingProgress.Stderr, SecretSanitizer.Redact(line)))),
                timeout.Token);
        }
        catch (CliExecutableNotFoundException ex)
        {
            logger.LogWarning(ex, "{Strategy} CLI is not installed.", DisplayName);
            return Failed(127, workingDirectory, commandLine, ex.Message);
        }
        catch (CodingWorkerNotInstalledException ex)
        {
            return Failed(127, workingDirectory, commandLine, ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(124, workingDirectory, commandLine, $"{DisplayName} timed out after {TimeoutSeconds} seconds.");
        }

        var (text, session) = JsonOutputReader.Extract(run.Stdout);
        var combined = $"{run.Stderr}{Environment.NewLine}{text}";
        var succeeded = run.ExitCode == 0;
        var error = succeeded
            ? null
            : SubscriptionAuth.LooksLikeAuthFailure(combined)
                ? SubscriptionAuth.NotLoggedInMessage(Kind, DisplayName)
                : (string.IsNullOrWhiteSpace(run.Stderr)
                    ? $"{DisplayName} exited {run.ExitCode}."
                    : $"{DisplayName} exited {run.ExitCode}. {run.Stderr.Trim()}");

        return new CodingTaskResult
        {
            Succeeded = succeeded,
            ExitCode = run.ExitCode,
            SessionId = session,
            FinalMessage = string.IsNullOrWhiteSpace(text) ? null : text,
            Error = error,
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            Strategy = DisplayName,
            Stderr = run.Stderr.Trim()
        };
    }

    private CodingAvailability Unsupported()
        => new()
        {
            Kind = Kind,
            IsInstalled = false,
            IsSupported = false,
            IsLoggedIn = false,
            ExecutablePath = ExecutablePath,
            LoginCommand = LoginCommand,
            Error = CodingHost.UnsupportedMessage(_host)
        };

    private CodingAvailability Missing(string error)
        => new()
        {
            Kind = Kind,
            IsInstalled = false,
            IsLoggedIn = false,
            ExecutablePath = ExecutablePath,
            LoginCommand = this.LoginCommand,
            Error = error
        };

    private CodingAvailability InstalledUnhealthy(string executablePath, string error)
        => new()
        {
            Kind = Kind,
            IsInstalled = true,
            IsLoggedIn = null,
            ExecutablePath = executablePath,
            LoginCommand = this.LoginCommand,
            Error = error
        };

    private string ResolvedDisplayPath()
        => CliExecutableResolver.TryResolve(ExecutablePath)?.Path ?? ExecutablePath;

    private CodingTaskResult Failed(int exitCode, string workingDirectory, string commandLine, string error)
        => new()
        {
            Succeeded = false,
            ExitCode = exitCode,
            Error = error,
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            Strategy = DisplayName
        };

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
}
