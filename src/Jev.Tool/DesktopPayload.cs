namespace Jev.Tool;

public static class DesktopPayload
{
    public static string Resolve(string toolDirectory, DesktopHost host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolDirectory);
        ArgumentNullException.ThrowIfNull(host);

        var path = Path.GetFullPath(Path.Combine(toolDirectory, "payload", host.Rid, host.ExecutableName));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Jev desktop payload for {host.Rid} was not packed next to the tool. Build with ./build.sh PackTool, then install the nupkg.",
                path);
        }

        return path;
    }
}
