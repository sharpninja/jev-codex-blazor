namespace Jev.Workers;

public sealed class CodingAvailability
{
    public required CodingStrategyKind Kind { get; init; }

    public required bool IsInstalled { get; init; }

    public string? Version { get; init; }

    public string ExecutablePath { get; init; } = "";

    public string? Error { get; init; }

    public string FormatForAgent()
        => IsInstalled
            ? $"{Kind} worker is available at '{ExecutablePath}'{(string.IsNullOrWhiteSpace(Version) ? "." : $": {Version}")}"
            : $"{Kind} worker is not available ({ExecutablePath}). {Error}";
}
