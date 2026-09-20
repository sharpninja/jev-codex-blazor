namespace Jev.Workers;

public sealed class CodingWorkerNotInstalledException : InvalidOperationException
{
    public CodingWorkerNotInstalledException(CodingStrategyKind kind, string executablePath, Exception? inner = null)
        : base(
            $"The {kind} coding worker was not found (looked for '{executablePath}'). " +
            "Install the CLI and ensure it is on PATH, or set the strategy's ExecutablePath.",
            inner)
    {
        Kind = kind;
        ExecutablePath = executablePath;
    }

    public CodingStrategyKind Kind { get; }

    public string ExecutablePath { get; }
}
