using Jev.Codex;

namespace Jev.Workers.Strategies;

public sealed class CodexCodingStrategy(ICodexCli codex, Microsoft.Extensions.Options.IOptions<CodexCliOptions> options)
    : ICodingAgentStrategy
{
    public CodingStrategyKind Kind => CodingStrategyKind.Codex;

    public string DisplayName => "Codex";

    public string ExecutablePath => options.Value.ExecutablePath;

    public string LoginCommand => CodexAvailability.LoginCommandText;

    public async Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var availability = await codex.ProbeAsync(cancellationToken);
        return new CodingAvailability
        {
            Kind = Kind,
            IsInstalled = availability.IsInstalled,
            IsLoggedIn = availability.IsLoggedIn,
            Version = availability.Version,
            ExecutablePath = availability.ExecutablePath,
            LoginCommand = availability.LoginCommand,
            Error = availability.Error
        };
    }

    public async Task<CodingTaskResult> RunAsync(
        CodingTaskRequest request,
        IProgress<CodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await codex.ExecAsync(
            new CodexExecRequest
            {
                Prompt = request.Prompt,
                WorkingDirectory = request.WorkingDirectory,
                Sandbox = request.Sandbox,
                Model = request.Model,
                SessionId = request.SessionId,
                CreateWorkspaceIfMissing = request.CreateWorkspaceIfMissing,
                Timeout = request.Timeout
            },
            progress is null ? null : new Progress<CodexProgress>(item => progress.Report(new CodingProgress(item.Phase, item.Message))),
            cancellationToken);

        return new CodingTaskResult
        {
            Succeeded = result.Succeeded,
            ExitCode = result.ExitCode,
            SessionId = result.ThreadId,
            FinalMessage = result.FinalMessage,
            Error = result.Error,
            CommandLine = result.CommandLine,
            WorkingDirectory = result.WorkingDirectory,
            Strategy = DisplayName,
            ChangedFiles = result.ChangedFiles,
            CommandsRun = result.CommandsRun,
            Stderr = result.Stderr
        };
    }
}
