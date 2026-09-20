namespace Jev.Workers.Cli;

public sealed class ClineCliOptions
{
    public const string SectionName = "Cline";

    /// <summary>
    /// CLI name or path. Leave as <c>cline</c> so Jev resolves PATH, Windows
    /// PATHEXT shims (<c>cline.cmd</c>), and npm global bins. Override with
    /// <c>Cline:ExecutablePath</c> or <c>CLINE_EXECUTABLE</c> only when auto-resolve fails.
    /// </summary>
    public string ExecutablePath { get; set; } = "cline";

    public string? Model { get; set; }

    /// <summary>
    /// Optional CLI provider override. Leave empty so Cline uses its default
    /// <c>cline</c> subscription provider from <c>cline auth</c>.
    /// </summary>
    public string? Provider { get; set; }

    public bool Yolo { get; set; } = true;

    public bool Json { get; set; } = true;

    public bool AutoApprove { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 300;

    public string? DefaultWorkspaceRoot { get; set; }
}
