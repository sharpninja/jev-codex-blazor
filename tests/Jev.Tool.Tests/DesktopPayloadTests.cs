using System.Runtime.InteropServices;
using Jev.Tool;

namespace Jev.Tool.Tests;

public sealed class DesktopPayloadTests
{
    [Fact]
    public void Resolve_returns_the_rid_binary()
    {
        var root = CreatePayload("linux-x64", "Jev");
        try
        {
            var path = DesktopPayload.Resolve(root, new DesktopHost("linux-x64", "Jev"));
            Assert.True(File.Exists(path));
            Assert.Equal("Jev", Path.GetFileName(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_throws_when_the_payload_was_not_packed()
    {
        var root = Directory.CreateTempSubdirectory("jev-tool-missing-").FullName;
        try
        {
            var ex = Assert.Throws<FileNotFoundException>(() =>
                DesktopPayload.Resolve(root, DesktopHost.TryDetect(OSPlatform.Linux, Architecture.X64)!));
            Assert.Contains("PackTool", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreatePayload(string rid, string fileName)
    {
        var root = Directory.CreateTempSubdirectory("jev-tool-payload-").FullName;
        var directory = Path.Combine(root, "payload", rid);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), "stub");
        return root;
    }
}
