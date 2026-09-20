namespace Jev.Codex;

public sealed class CodexAvailability
{
    public required bool IsInstalled { get; init; }

    public string? Version { get; init; }

    public string ExecutablePath { get; init; } = "codex";

    public string? Error { get; init; }

    public string FormatForAgent()
        => IsInstalled
            ? $"Codex CLI is available at '{ExecutablePath}'{(string.IsNullOrWhiteSpace(Version) ? "." : $": {Version}")}"
            : $"Codex CLI is not available ({ExecutablePath}). {Error}";
}
