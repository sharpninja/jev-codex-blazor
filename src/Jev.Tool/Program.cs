using System.Diagnostics;
using System.Runtime.InteropServices;
using Jev.Tool;

var toolDirectory = AppContext.BaseDirectory;
DesktopHost host;
string executable;
try
{
    host = DesktopHost.DetectCurrent();
    executable = DesktopPayload.Resolve(toolDirectory, host);
}
catch (Exception ex) when (ex is PlatformNotSupportedException or FileNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return ex is PlatformNotSupportedException ? 64 : 66;
}

if (args is ["--tool-info", ..])
{
    Console.WriteLine($"host={host.Rid}");
    Console.WriteLine($"binary={executable}");
    Console.WriteLine($"os={RuntimeInformation.OSDescription}");
    return 0;
}

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    TryChmodExecute(executable);
}

var start = new ProcessStartInfo
{
    FileName = executable,
    UseShellExecute = false,
    WorkingDirectory = Directory.GetCurrentDirectory()
};
foreach (var argument in args)
{
    start.ArgumentList.Add(argument);
}

using var process = Process.Start(start);
if (process is null)
{
    Console.Error.WriteLine($"Failed to start {executable}.");
    return 127;
}

process.WaitForExit();
return process.ExitCode;

static void TryChmodExecute(string path)
{
    try
    {
        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            ArgumentList = { "+x", path },
            UseShellExecute = false
        });
        chmod?.WaitForExit();
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
    {
        // The published ELF usually already has +x; chmod is a fallback for zip-extracted payloads.
    }
}
