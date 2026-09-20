namespace Jev.Core;

public sealed class JevAgentOptions
{
    public const string SectionName = "Jev";

    public string Name { get; set; } = "Jev";

    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Directory containing jev-system.md and jev-policies.md. Empty = probe well-known paths.</summary>
    public string? PersonaDirectory { get; set; }

    /// <summary>Force the local fallback IChatClient even when an OpenAI key is present.</summary>
    public bool UseFallbackChatClient { get; set; }

    /// <summary>Codex | Claude | GrokBuild | Cline</summary>
    public string CodingStrategy { get; set; } = "Codex";
}
