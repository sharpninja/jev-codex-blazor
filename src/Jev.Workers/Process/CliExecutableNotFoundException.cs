namespace Jev.Workers.Process;

public sealed class CliExecutableNotFoundException : InvalidOperationException
{
    public CliExecutableNotFoundException(string executablePath, Exception? inner = null)
        : base(
            $"CLI executable '{executablePath}' was not found on this machine. " +
            "Searched PATH, Windows PATHEXT shims (.exe, .cmd, .bat, .ps1), and common npm global bins. " +
            "If it is installed, set the worker ExecutablePath or CODEX_EXECUTABLE / CLAUDE_EXECUTABLE / GROK_EXECUTABLE / CLINE_EXECUTABLE.",
            inner)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}
