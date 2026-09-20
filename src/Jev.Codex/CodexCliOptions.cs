namespace Jev.Codex;

public sealed class CodexCliOptions
{
    public const string SectionName = "Codex";

    /// <summary>CLI entrypoint. Defaults to <c>codex</c> on PATH.</summary>
    public string ExecutablePath { get; set; } = "codex";

    /// <summary>read-only | workspace-write | danger-full-access</summary>
    public string DefaultSandbox { get; set; } = "workspace-write";

    /// <summary>untrusted | on-request | never. Non-interactive runs should use never.</summary>
    public string AskForApproval { get; set; } = "never";

    public bool SkipGitRepoCheck { get; set; } = true;

    public bool UseJsonEvents { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Optional default workspace for coding tasks. Empty means create a temp folder.</summary>
    public string? DefaultWorkspaceRoot { get; set; }

    public string? Model { get; set; }

    /// <summary>When false, requests for danger-full-access are clamped to workspace-write.</summary>
    public bool AllowDangerousSandbox { get; set; }
}
