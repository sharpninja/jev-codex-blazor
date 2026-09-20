namespace Jev.Codex;

public sealed class CodexNotInstalledException : InvalidOperationException
{
    public CodexNotInstalledException(string executablePath, Exception? inner = null)
        : base(
            $"The Codex CLI was not found (looked for '{executablePath}'). " +
            "Install the OpenAI Codex CLI and ensure it is on PATH, or set Codex:ExecutablePath / CODEX_EXECUTABLE.",
            inner)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}
