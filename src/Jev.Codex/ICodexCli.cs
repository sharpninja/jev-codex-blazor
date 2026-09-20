namespace Jev.Codex;

public interface ICodexCli
{
    Task<CodexAvailability> ProbeAsync(CancellationToken cancellationToken = default);

    Task<CodexExecResult> ExecAsync(
        CodexExecRequest request,
        IProgress<CodexProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
