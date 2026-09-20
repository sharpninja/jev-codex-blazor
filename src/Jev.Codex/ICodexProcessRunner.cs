using System.Diagnostics;

namespace Jev.Codex;

public interface ICodexProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        IProgress<string>? stdoutLine,
        IProgress<string>? stderrLine,
        CancellationToken cancellationToken);
}
