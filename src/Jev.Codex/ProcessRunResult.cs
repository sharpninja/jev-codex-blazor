namespace Jev.Codex;

public sealed class ProcessRunResult
{
    public required int ExitCode { get; init; }

    public string Stdout { get; init; } = "";

    public string Stderr { get; init; } = "";
}
