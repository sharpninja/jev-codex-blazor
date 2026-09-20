namespace Jev.Workers;

public sealed class CodingTaskRequest
{
    public required string Prompt { get; init; }

    public string? WorkingDirectory { get; init; }

    public string? Sandbox { get; init; }

    public string? Model { get; init; }

    public string? SessionId { get; init; }

    public bool CreateWorkspaceIfMissing { get; init; } = true;

    public TimeSpan? Timeout { get; init; }
}
