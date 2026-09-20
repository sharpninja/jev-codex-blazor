using System.ComponentModel;
using Jev.Core.Runtime;
using Jev.Workers;
using Jev.Workers.Process;

namespace Jev.Core.Tools;

public sealed class JevCodingTools(
    ICodingStrategySelector selector,
    IJevRunStatus status,
    ICliTranscript? transcript = null)
{
    public const string ProbeName = "probe_coding_worker";
    public const string RunCodingTaskName = "run_coding_task";
    public const string ScaffoldHelloConsoleName = "scaffold_hello_console";

    [Description("Check whether the selected coding-worker CLI (Codex, Claude Code, Grok Build, or Cline) is installed and signed in with a subscription (codex login / claude auth login / grok login / cline auth).")]
    public async Task<string> ProbeCodingWorkerAsync(CancellationToken cancellationToken)
    {
        var worker = selector.Active;
        status.SetPhase(JevPhase.RunningWorker, $"Probing {worker.DisplayName}");
        transcript?.Append(CliTranscriptChannel.System, $"Probing {worker.DisplayName}");
        var availability = await worker.ProbeAsync(cancellationToken);
        status.SetWorkerAvailability(
            availability.IsInstalled,
            availability.Version,
            availability.IsLoggedIn,
            availability.LoginCommand);
        if (!availability.IsSupported || !string.IsNullOrWhiteSpace(availability.Error))
        {
            transcript?.Append(CliTranscriptChannel.System, availability.Error ?? availability.FormatForAgent());
        }

        return availability.FormatForAgent();
    }

    [Description("Delegate a coding task to the selected coding-worker strategy. Use this to create, edit, test, or inspect files. Do not use for small talk.")]
    public Task<string> RunCodingTaskAsync(
        [Description("Self-contained instruction for the coding worker, including the goal, constraints, and expected artifacts.")]
        string prompt,
        [Description("Workspace directory. Leave empty to use a new temp workspace.")]
        string workingDirectory = "",
        [Description("Optional sandbox hint for workers that support one (Codex: read-only, workspace-write, danger-full-access).")]
        string sandbox = "",
        [Description("Optional worker session id to resume.")]
        string sessionId = "",
        CancellationToken cancellationToken = default)
        => ExecAsync(prompt, NullIfEmpty(workingDirectory), NullIfEmpty(sandbox), NullIfEmpty(sessionId), cancellationToken);

    [Description("Demo path: scaffold a hello console app in a fresh temp workspace via the selected coding worker.")]
    public Task<string> ScaffoldHelloConsoleAsync(
        [Description("Optional workspace directory. Leave empty to create a temp folder.")]
        string workingDirectory = "",
        CancellationToken cancellationToken = default)
    {
        const string prompt = """
            Scaffold a tiny hello-world console application in this empty workspace.

            Requirements:
            - Prefer `dotnet new console` if the .NET SDK is available; otherwise write a minimal program in a common language.
            - The program must print Hello from Jev when run.
            - Do not use destructive commands.
            - Keep the change set small and explain the files you created.
            """;

        return ExecAsync(prompt, NullIfEmpty(workingDirectory), "workspace-write", sessionId: null, cancellationToken);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task<string> ExecAsync(
        string prompt,
        string? workingDirectory,
        string? sandbox,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var worker = selector.Active;
        status.SetPhase(JevPhase.RunningWorker, $"Starting {worker.DisplayName}");
        var result = await worker.RunAsync(
            new CodingTaskRequest
            {
                Prompt = prompt,
                WorkingDirectory = workingDirectory,
                Sandbox = sandbox,
                SessionId = sessionId
            },
            new ImmediateProgress<CodingProgress>(item =>
            {
                if (item.Phase is not (CodingProgress.Input or CodingProgress.Stdout or CodingProgress.Stderr))
                {
                    status.ReportWorker(item);
                }

                transcript?.Append(item);
            }),
            cancellationToken);

        if (!result.Succeeded)
        {
            status.SetPhase(JevPhase.Error, result.Error);
        }

        return result.FormatForAgent();
    }
}
