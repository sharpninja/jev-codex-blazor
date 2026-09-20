using System.Runtime.InteropServices;
using Jev.Tool;

namespace Jev.Tool.Tests;

public sealed class DesktopHostTests
{
    [Fact]
    public void Windows_x64_uses_the_win_payload()
    {
        var host = DesktopHost.TryDetect(OSPlatform.Windows, Architecture.X64);
        Assert.NotNull(host);
        Assert.Equal("win-x64", host.Rid);
        Assert.Equal("Jev.exe", host.ExecutableName);
    }

    [Fact]
    public void Linux_x64_uses_the_linux_payload()
    {
        var host = DesktopHost.TryDetect(OSPlatform.Linux, Architecture.X64);
        Assert.NotNull(host);
        Assert.Equal("linux-x64", host.Rid);
        Assert.Equal("Jev", host.ExecutableName);
    }

    [Theory]
    [InlineData("OSX")]
    [InlineData("FreeBSD")]
    public void Other_operating_systems_are_unsupported(string name)
    {
        Assert.Null(DesktopHost.TryDetect(OSPlatform.Create(name), Architecture.X64));
    }

    [Fact]
    public void Arm64_is_unsupported()
    {
        Assert.Null(DesktopHost.TryDetect(OSPlatform.Linux, Architecture.Arm64));
        Assert.Null(DesktopHost.TryDetect(OSPlatform.Windows, Architecture.Arm64));
    }
}
