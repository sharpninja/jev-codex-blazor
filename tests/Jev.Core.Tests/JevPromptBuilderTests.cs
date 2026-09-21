using Jev.Core.Simulation;

namespace Jev.Core.Tests;

public sealed class JevPromptBuilderTests
{
    [Fact]
    public void Who_are_you_includes_persona_and_user_text_without_fallback_router()
    {
        var prompt = JevPromptBuilder.Build(
            "You are Jev, a coding-assistant persona simulated by this CLI.",
            "Codex",
            [],
            "who are you?");

        Assert.Contains("You are Jev", prompt, StringComparison.Ordinal);
        Assert.Contains("who are you?", prompt, StringComparison.Ordinal);
        Assert.Contains("Codex", prompt, StringComparison.Ordinal);
        Assert.Contains("no separate orchestration model", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback router because no orchestration", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPENAI_API_KEY", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Includes_recent_history_and_caps_older_turns()
    {
        var history = Enumerable.Range(1, 20)
            .Select(i => new JevHistoryTurn(i % 2 == 1 ? "user" : "jev", $"turn-{i}"))
            .ToList();

        var prompt = JevPromptBuilder.Build("persona", "Claude Code", history, "continue");

        Assert.Contains("turn-20", prompt, StringComparison.Ordinal);
        Assert.Contains("turn-5", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-4", prompt, StringComparison.Ordinal);
        Assert.Contains("## Conversation so far", prompt, StringComparison.Ordinal);
        Assert.Contains("## Current user message", prompt, StringComparison.Ordinal);
        Assert.Contains("continue", prompt, StringComparison.Ordinal);
    }
}
