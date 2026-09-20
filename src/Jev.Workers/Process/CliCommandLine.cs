namespace Jev.Workers.Process;

public static class CliCommandLine
{
    public static string Format(string executable, IEnumerable<string> arguments)
        => string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote));

    public static string Quote(string value)
        => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
