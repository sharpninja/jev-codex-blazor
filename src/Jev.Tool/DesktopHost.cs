using System.Runtime.InteropServices;

namespace Jev.Tool;

public sealed record DesktopHost(string Rid, string ExecutableName)
{
    public static DesktopHost? TryDetect(OSPlatform operatingSystem, Architecture architecture)
    {
        if (architecture != Architecture.X64)
        {
            return null;
        }

        if (operatingSystem == OSPlatform.Windows)
        {
            return new DesktopHost("win-x64", "Jev.exe");
        }

        if (operatingSystem == OSPlatform.Linux)
        {
            return new DesktopHost("linux-x64", "Jev");
        }

        return null;
    }

    public static DesktopHost DetectCurrent()
    {
        OSPlatform os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            os = OSPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            os = OSPlatform.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            os = OSPlatform.OSX;
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"Jev has no desktop payload for {RuntimeInformation.OSDescription} / {RuntimeInformation.OSArchitecture}.");
        }

        return TryDetect(os, RuntimeInformation.OSArchitecture)
            ?? throw new PlatformNotSupportedException(
                $"Jev ships Windows and Linux x64 Photino binaries. This host is {os} / {RuntimeInformation.OSArchitecture}.");
    }
}
