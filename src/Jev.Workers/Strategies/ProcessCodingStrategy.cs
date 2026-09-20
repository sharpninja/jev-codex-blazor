using System.Diagnostics;
using Jev.Workers.Process;
using Microsoft.Extensions.Logging;

namespace Jev.Workers.Strategies;

public abstract class ProcessCodingStrategy(
    ICliProcessRunner processRunner,
    ILogger logger) : ICodingAgentStrategy
{
    public abstract CodingStrategyKind Kind { get; }

    public abstract string DisplayName { get; }

    public abstract string ExecutablePath { get; }

    protected abstract int TimeoutSeconds { get; }

    protected abstract string? DefaultWorkspaceRoot { get; }

    protected abstract IReadOnlyList<string> BuildVersionArguments();

    protected abstract IReadOnlyList<string> BuildExecArguments(CodingTaskRequest request, string workingDirectory);

    protected virtual string? StandardInput(CodingTaskRequest request) => null;

    public async Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = CreateStartInfo(ExecutablePath, BuildVersionArguments(), Directory.GetCurrentDirectory());
            var run = await processRunner.RunAsync(startInfo, null, null, cancellationToken);
            var version = (run.Stdout + " " + run.Stderr).Trim().ReplaceLineEndings(" ");
            var installed = run.ExitCode == 0;
            return new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = installed,
                Version = string.IsNullOrWhiteSpace(version) ? null : version,
                ExecutablePath = ExecutablePath,
                Error = installed ? null : $"'{ExecutablePath} --version' exited {run.ExitCode}."
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
        var workingDirectory = WorkspacePath.Ensure(request.WorkingDirectory, DefaultWorkspaceRoot, request.CreateWorkspaceIfMissing);
        var arguments = BuildExecArguments(request, workingDirectory);
        var commandLine = CliCommandLine.Format(ExecutablePath, arguments);
        progress?.Report(new CodingProgress("starting", $"Starting {DisplayName} in {workingDirectory}"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds)));

        CliProcessRunResult run;
        try
        {
            var startInfo = CreateStartInfo(ExecutablePath, arguments, workingDirectory);
            run = await processRunner.RunAsync(
                startInfo,
                StandardInput(request),
                new Progress<string>(line => progress?.Report(new CodingProgress("stdout", line))),
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
        var succeeded = run.ExitCode == 0;
        return new CodingTaskResult
        {
            Succeeded = succeeded,
            ExitCode = run.ExitCode,
            SessionId = session,
            FinalMessage = string.IsNullOrWhiteSpace(text) ? null : text,
            Error = succeeded ? null : (string.IsNullOrWhiteSpace(run.Stderr) ? $"{DisplayName} exited {run.ExitCode}." : run.Stderr.Trim()),
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            Strategy = DisplayName,
            Stderr = run.Stderr.Trim()
        };
    }

    private CodingAvailability Missing(string error)
        => new()
        {
            Kind = Kind,
            IsInstalled = false,
            ExecutablePath = ExecutablePath,
            Error = error
        };

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
