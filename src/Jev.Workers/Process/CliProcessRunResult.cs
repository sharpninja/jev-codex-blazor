namespace Jev.Workers.Process;

public sealed class CliProcessRunResult
{
    public required int ExitCode { get; init; }

    public string Stdout { get; init; } = "";

    public string Stderr { get; init; } = "";
}
