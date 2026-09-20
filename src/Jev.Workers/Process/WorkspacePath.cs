namespace Jev.Workers.Process;

public static class WorkspacePath
{
    public static string Ensure(string? requested, string? defaultRoot, bool createIfMissing)
    {
        var path = requested;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = string.IsNullOrWhiteSpace(defaultRoot)
                ? Path.Combine(Path.GetTempPath(), "jev-workspaces", Guid.NewGuid().ToString("n")[..8])
                : defaultRoot;
        }

        path = Path.GetFullPath(path);
        if (createIfMissing && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Coding workspace '{path}' does not exist.");
        }

        return path;
    }
}
