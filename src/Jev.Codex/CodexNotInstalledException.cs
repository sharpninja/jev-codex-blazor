namespace Jev.Codex;

public sealed class CodexNotInstalledException : InvalidOperationException
{
    public CodexNotInstalledException(string executablePath, Exception? inner = null)
        : base(
            $"The Codex CLI was not found (looked for '{executablePath}'). " +
            "Searched PATH, Windows PATHEXT shims (.exe, .cmd, .bat, .ps1), and common npm global bins. " +
            "Install the OpenAI Codex CLI, or set Codex:ExecutablePath / CODEX_EXECUTABLE.",
            inner)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}
