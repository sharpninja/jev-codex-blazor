namespace Jev.Workers.Cli;

public sealed class GrokBuildCliOptions
{
    public const string SectionName = "GrokBuild";

    public string ExecutablePath { get; set; } = "grok";

    public string? Model { get; set; }

    public string OutputFormat { get; set; } = "streaming-json";

    public bool AlwaysApprove { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 300;

    public string? DefaultWorkspaceRoot { get; set; }
}
