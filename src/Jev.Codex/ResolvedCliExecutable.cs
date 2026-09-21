namespace Jev.Codex;

public enum CliLaunchKind
{
    Native,
    WindowsCmdScript,
    WindowsPowerShellScript
}

/// <summary>
/// A CLI found on disk plus how to start it when <c>UseShellExecute = false</c>
/// (Windows <c>.cmd</c>/<c>.bat</c>/<c>.ps1</c> shims cannot be CreateProcess'd directly).
/// </summary>
public sealed record ResolvedCliExecutable(
    string Command,
    string Path,
    string FileName,
    CliLaunchKind Kind);
