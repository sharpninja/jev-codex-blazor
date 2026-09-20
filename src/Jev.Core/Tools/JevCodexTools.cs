using System.ComponentModel;
using Jev.Codex;
using Jev.Core.Runtime;

namespace Jev.Core.Tools;

public sealed class JevCodexTools(ICodexCli codex, IJevRunStatus status)
{
    public const string ProbeName = "probe_codex";
    public const string RunCodingTaskName = "run_coding_task";
    public const string ScaffoldHelloConsoleName = "scaffold_hello_console";

    [Description("Check whether the Codex CLI is installed and report its version.")]
    public async Task<string> ProbeCodexAsync(CancellationToken cancellationToken)
    {
        status.SetPhase(JevPhase.RunningCodex, "Probing Codex CLI");
        var availability = await codex.ProbeAsync(cancellationToken);
        status.SetCodexAvailability(availability.IsInstalled, availability.Version);
        return availability.FormatForAgent();
    }

    [Description("Delegate a coding task to the Codex CLI in a workspace. Use this to create, edit, test, or inspect files. Do not use for small talk.")]
    public Task<string> RunCodingTaskAsync(
        [Description("Self-contained instruction for Codex, including the goal, constraints, and expected artifacts.")]
        string prompt,
        [Description("Workspace directory. Leave empty to use a new temp workspace.")]
        string workingDirectory = "",
        [Description("Optional Codex sandbox: read-only, workspace-write, or danger-full-access.")]
        string sandbox = "",
        [Description("Optional Codex thread id to resume.")]
        string sessionId = "",
        CancellationToken cancellationToken = default)
        => ExecAsync(
            prompt,
            NullIfEmpty(workingDirectory),
            NullIfEmpty(sandbox),
            NullIfEmpty(sessionId),
            cancellationToken);

    [Description("Demo path: scaffold a hello console app in a fresh temp workspace via Codex.")]
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

        return ExecAsync(
            prompt,
            NullIfEmpty(workingDirectory),
            CodexSandbox.WorkspaceWrite,
            sessionId: null,
            cancellationToken);
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
        status.SetPhase(JevPhase.RunningCodex, "Starting Codex");
        var result = await codex.ExecAsync(
            new CodexExecRequest
            {
                Prompt = prompt,
                WorkingDirectory = workingDirectory,
                Sandbox = sandbox,
                SessionId = sessionId
            },
            new Progress<CodexProgress>(status.ReportCodex),
            cancellationToken);

        if (!result.Succeeded)
        {
            status.SetPhase(JevPhase.Error, result.Error);
        }

        return result.FormatForAgent();
    }
}
