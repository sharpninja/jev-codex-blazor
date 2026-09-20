namespace Jev.Core;

/// <summary>
/// Optional Jev <em>orchestration</em> LLM. Not used by Codex / Claude / Grok / Cline workers.
/// Those CLIs authenticate with subscription login.
/// </summary>
public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";
}
