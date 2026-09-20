namespace Jev.Workers;

/// <summary>
/// Host capabilities for coding-worker CLIs. Desktop Windows/Linux can spawn
/// local CLIs; WASM and Android cannot.
/// </summary>
public interface ICodingHost
{
    string Name { get; }

    bool SupportsLocalCli { get; }
}

public sealed record CodingHostInfo(string Name, bool SupportsLocalCli) : ICodingHost;

public static class CodingHost
{
    public static ICodingHost Desktop(string name = "desktop") => new CodingHostInfo(name, true);

    public static ICodingHost Restricted(string name) => new CodingHostInfo(name, false);

    public static string UnsupportedMessage(ICodingHost host)
        => $"Coding-worker CLIs are not supported on this host ({host.Name}). Use the Windows or Linux desktop app, or the ASP.NET host on a machine with Codex / Claude / Grok / Cline installed.";
}
