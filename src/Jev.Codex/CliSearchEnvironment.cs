namespace Jev.Codex;

/// <summary>
/// Search context for <see cref="CliExecutableResolver"/>. Tests inject PATH/PATHEXT
/// so Windows shim behavior can be verified on Linux CI.
/// </summary>
public sealed record CliSearchEnvironment
{
    public string Path { get; init; } = "";

    /// <summary>Windows PATHEXT (e.g. <c>.COM;.EXE;.BAT;.CMD</c>). Ignored when <see cref="IsWindows"/> is false.</summary>
    public string? PathExt { get; init; }

    public bool IsWindows { get; init; }

    /// <summary>Additional directories to search after PATH (npm prefix fixtures, etc.).</summary>
    public IReadOnlyList<string> ExtraDirectories { get; init; } = [];

    /// <summary>Search <c>%AppData%\npm</c>, <c>~/.npm-global/bin</c>, Volta, and similar well-known bins.</summary>
    public bool IncludeWellKnownDirectories { get; init; } = true;

    /// <summary>When the CLI is still missing, run <c>npm bin -g</c> / <c>npm prefix -g</c> once and cache the result.</summary>
    public bool QueryNpmPrefix { get; init; } = true;

    public static CliSearchEnvironment Current => new()
    {
        Path = Environment.GetEnvironmentVariable("PATH") ?? "",
        PathExt = Environment.GetEnvironmentVariable("PATHEXT"),
        IsWindows = OperatingSystem.IsWindows(),
        IncludeWellKnownDirectories = true,
        QueryNpmPrefix = true
    };
}
