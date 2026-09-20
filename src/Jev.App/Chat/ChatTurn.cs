namespace Jev.App.Chat;

public sealed class ChatTurn
{
    public string Id { get; } = Guid.NewGuid().ToString("n");

    public required string Role { get; init; }

    public string Text { get; set; } = "";

    public bool IsStreaming { get; set; }

    public bool IsError { get; set; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
