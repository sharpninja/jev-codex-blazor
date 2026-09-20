namespace Jev.Workers;

public enum CodingStrategyKind
{
    Codex,
    Claude,
    GrokBuild,
    Cline
}

public static class CodingStrategyKindParser
{
    public static bool TryParse(string? value, out CodingStrategyKind kind)
    {
        kind = CodingStrategyKind.Codex;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant())
        {
            case "codex":
                kind = CodingStrategyKind.Codex;
                return true;
            case "claude":
            case "claudecode":
            case "anthropic":
                kind = CodingStrategyKind.Claude;
                return true;
            case "grok":
            case "grokbuild":
            case "xai":
                kind = CodingStrategyKind.GrokBuild;
                return true;
            case "cline":
                kind = CodingStrategyKind.Cline;
                return true;
            default:
                return false;
        }
    }

    public static CodingStrategyKind ParseOrDefault(string? value, CodingStrategyKind fallback = CodingStrategyKind.Codex)
        => TryParse(value, out var kind) ? kind : fallback;
}
