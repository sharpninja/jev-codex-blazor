namespace Jev.Workers;

public sealed record CodingProgress(string Phase, string Message)
{
    public const string Starting = "starting";
    public const string Input = "input";
    public const string Stdout = "stdout";
    public const string Stderr = "stderr";
    public const string System = "system";
}
