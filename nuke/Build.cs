using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter] public readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath SolutionFile => RootDirectory / "Jev.slnx";
    AbsolutePath WasmProject => RootDirectory / "src" / "Jev.Wasm" / "Jev.Wasm.csproj";
    AbsolutePath LinuxProject => RootDirectory / "src" / "Jev.Linux" / "Jev.Linux.csproj";
    AbsolutePath MauiProject => RootDirectory / "src" / "Jev.Maui" / "Jev.Maui.csproj";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (Directory.Exists(ArtifactsDirectory))
            {
                Directory.Delete(ArtifactsDirectory, recursive: true);
            }

            Directory.CreateDirectory(ArtifactsDirectory);
            DotNetClean(s => s.SetProject(SolutionFile));
        });

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s.SetProjectFile(SolutionFile)));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoBuild()));

    Target PublishWasm => _ => _
        .DependsOn(Compile)
        .Executes(PublishWasmCore);

    Target PublishLinux => _ => _
        .DependsOn(Compile)
        .Executes(PublishLinuxCore);

    Target PublishWindows => _ => _
        .Executes(PublishWindowsCore);

    Target PublishAndroid => _ => _
        .Executes(PublishAndroidCore);

    Target Pack => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            PublishWasmCore();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                PublishLinuxCore();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PublishWindowsCore();
            }

            Log.Information("Pack complete for this OS. Use PackAll to require every platform SDK.");
        });

    Target PackAll => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            PublishWasmCore();
            PublishLinuxCore();
            PublishWindowsCore();
            PublishAndroidCore();
        });

    void PublishWasmCore()
    {
        var output = ArtifactsDirectory / "wasm" / "wwwroot";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(WasmProject)
            .SetConfiguration(Configuration.Release)
            .SetOutput(output));
        ZipDirectory(output, ArtifactsDirectory / "jev-wasm.zip");
        Log.Information("WASM package: {Zip}", ArtifactsDirectory / "jev-wasm.zip");
    }

    void PublishLinuxCore()
    {
        var output = ArtifactsDirectory / "linux" / "app";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(LinuxProject)
            .SetConfiguration(Configuration.Release)
            .SetRuntime("linux-x64")
            .SetSelfContained(true)
            .SetOutput(output));
        var tarball = ArtifactsDirectory / "jev-linux-x64.tar.gz";
        CreateTarball(output, tarball);
        Log.Information("Linux package: {Tar}", tarball);
    }

    void PublishWindowsCore()
    {
        RequireWindows("PublishWindows");
        RequireMauiWorkload("maui-windows", "PublishWindows");
        var output = ArtifactsDirectory / "windows" / "app";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(MauiProject)
            .SetConfiguration(Configuration.Release)
            .SetFramework("net10.0-windows10.0.19041.0")
            .SetProperty("WindowsPackageType", "None")
            .SetOutput(output));
        ZipDirectory(output, ArtifactsDirectory / "jev-windows-x64.zip");
        Log.Information("Windows unpackaged zip: {Zip}", ArtifactsDirectory / "jev-windows-x64.zip");
        Log.Information("MSIX: on Windows, republish with -p:WindowsPackageType=MSIX when the Windows App SDK is installed.");
    }

    void PublishAndroidCore()
    {
        RequireMauiWorkload("maui-android", "PublishAndroid");
        RequireAndroidSdk();
        var output = ArtifactsDirectory / "android";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(MauiProject)
            .SetConfiguration(Configuration.Release)
            .SetFramework("net10.0-android")
            .SetProperty("AndroidPackageFormat", "apk")
            .SetOutput(output));

        var packages = Directory.GetFiles(output, "*.apk", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(output, "*.aab", SearchOption.AllDirectories))
            .ToArray();
        if (packages.Length == 0)
        {
            Assert.Fail("Android publish finished but no .apk/.aab was produced. Check the Android SDK / signing setup.");
        }

        foreach (var package in packages)
        {
            var dest = ArtifactsDirectory / Path.GetFileName(package);
            File.Copy(package, dest, overwrite: true);
            Log.Information("Android package: {Package}", dest);
        }
    }

    void RequireWindows(string target)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Fail($"{target} requires Windows (MAUI WinUI / BlazorWebView). This machine is {RuntimeInformation.OSDescription}.");
        }
    }

    void RequireMauiWorkload(string workload, string target)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "workload list",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start dotnet workload list.");
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (!stdout.Contains(workload, StringComparison.OrdinalIgnoreCase)
            && !stdout.Contains("maui", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{target} requires the .NET MAUI workload ({workload}). Install with: dotnet workload install {workload}");
        }
    }

    void RequireAndroidSdk()
    {
        var sdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
                  ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (string.IsNullOrWhiteSpace(sdk) || !Directory.Exists(sdk))
        {
            Assert.Fail("PublishAndroid requires ANDROID_SDK_ROOT or ANDROID_HOME pointing at an Android SDK.");
        }
    }

    static void PrepareDirectory(AbsolutePath path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    static void ZipDirectory(AbsolutePath source, AbsolutePath zip)
    {
        if (File.Exists(zip))
        {
            File.Delete(zip);
        }

        Directory.CreateDirectory(zip.Parent);
        ZipFile.CreateFromDirectory(source, zip);
    }

    static void CreateTarball(AbsolutePath source, AbsolutePath tarball)
    {
        if (File.Exists(tarball))
        {
            File.Delete(tarball);
        }

        Directory.CreateDirectory(tarball.Parent);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var zip = tarball.Parent / "jev-linux-x64.zip";
            ZipDirectory(source, zip);
            Log.Warning("tar is not used on Windows; wrote {Zip} instead of {Tar}", zip, tarball);
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-czf \"{tarball}\" -C \"{source}\" .",
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start tar.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Fail($"tar exited {process.ExitCode} while creating {tarball}.");
        }
    }
}
