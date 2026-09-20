using System.Diagnostics;

namespace Jev.Workers.Process;

public interface ICliProcessRunner
{
    Task<CliProcessRunResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        IProgress<string>? stdoutLine,
        CancellationToken cancellationToken);
}
