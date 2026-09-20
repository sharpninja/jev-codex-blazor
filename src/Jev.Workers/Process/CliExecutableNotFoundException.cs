namespace Jev.Workers.Process;

public sealed class CliExecutableNotFoundException : InvalidOperationException
{
    public CliExecutableNotFoundException(string executablePath, Exception? inner = null)
        : base($"CLI executable '{executablePath}' was not found on this machine.", inner)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}
