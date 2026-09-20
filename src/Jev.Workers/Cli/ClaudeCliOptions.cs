namespace Jev.Workers.Cli;

public sealed class ClaudeCliOptions
{
    public const string SectionName = "Claude";

    public string ExecutablePath { get; set; } = "claude";

    public string? Model { get; set; }

    public string PermissionMode { get; set; } = "acceptEdits";

    public string AllowedTools { get; set; } = "Read,Edit,Bash";

    public string OutputFormat { get; set; } = "json";

    public int TimeoutSeconds { get; set; } = 300;

    public string? DefaultWorkspaceRoot { get; set; }
}
