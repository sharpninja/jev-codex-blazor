namespace Jev.Codex;

public sealed class CodexJsonEvent
{
    public required string Type { get; init; }

    public string? ThreadId { get; init; }

    public string? ItemType { get; init; }

    public string? ItemId { get; init; }

    public string? Text { get; init; }

    public string? Command { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    public string Raw { get; init; } = "";

    public bool IsAgentMessage =>
        string.Equals(ItemType, "agent_message", StringComparison.OrdinalIgnoreCase);

    public bool IsProgress =>
        Type.StartsWith("item.", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "turn.started", StringComparison.OrdinalIgnoreCase);
}
