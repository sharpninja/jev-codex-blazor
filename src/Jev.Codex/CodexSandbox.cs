namespace Jev.Codex;

public static class CodexSandbox
{
    public const string ReadOnly = "read-only";
    public const string WorkspaceWrite = "workspace-write";
    public const string DangerFullAccess = "danger-full-access";

    public static string Normalize(string? value, string fallback = WorkspaceWrite)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim() switch
        {
            ReadOnly or "read_only" or "readonly" => ReadOnly,
            WorkspaceWrite or "workspace_write" or "write" => WorkspaceWrite,
            DangerFullAccess or "danger" or "yolo" or "full-auto" => DangerFullAccess,
            _ => fallback
        };
    }

    public static string Clamp(string sandbox, bool allowDangerous)
        => sandbox == DangerFullAccess && !allowDangerous ? WorkspaceWrite : sandbox;
}
