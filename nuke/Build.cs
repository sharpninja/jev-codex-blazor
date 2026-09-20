using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter] public readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter] public readonly string Version = "0.1.0";

    [Parameter] public readonly string ReleaseTag = "v0.1.0";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath SolutionFile => RootDirectory / "Jev.slnx";
    AbsolutePath WasmProject => RootDirectory / "src" / "Jev.Wasm" / "Jev.Wasm.csproj";
    AbsolutePath LinuxProject => RootDirectory / "src" / "Jev.Linux" / "Jev.Linux.csproj";
    AbsolutePath MauiProject => RootDirectory / "src" / "Jev.Maui" / "Jev.Maui.csproj";
    AbsolutePath IconSvg => RootDirectory / "src" / "Jev.Maui" / "Resources" / "AppIcon" / "appicon.svg";

    AbsolutePath WasmZip => ArtifactsDirectory / "jev-wasm.zip";
    AbsolutePath LinuxTarball => ArtifactsDirectory / "jev-linux-x64.tar.gz";
    AbsolutePath LinuxDeb => ArtifactsDirectory / $"jev_{Version}_amd64.deb";
    AbsolutePath WindowsPortableZip => ArtifactsDirectory / "jev-windows-x64.zip";
    AbsolutePath ChecksumsFile => ArtifactsDirectory / "SHA256SUMS";

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

    Target PublishWindowsPortable => _ => _
        .DependsOn(Compile)
        .Executes(PublishWindowsPortableCore);

    Target PublishWindows => _ => _
        .Executes(PublishWindowsMauiCore);

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
                PublishWindowsPortableCore();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PublishWindowsMauiCore();
            }

            WriteChecksums();
            Log.Information("Pack complete for this OS. Use PackAll to require every platform SDK.");
        });

    Target PackAll => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            PublishWasmCore();
            PublishLinuxCore();
            PublishWindowsPortableCore();
            PublishWindowsMauiCore();
            PublishAndroidCore();
            WriteChecksums();
        });

    Target Release => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            PublishWasmCore();
            PublishLinuxCore();
            PublishWindowsPortableCore();
            WriteChecksums();
            WriteReleaseNotes();
            CreateGitHubRelease();
        });

    void PublishWasmCore()
    {
        var output = ArtifactsDirectory / "wasm";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(WasmProject)
            .SetConfiguration(Configuration.Release)
            .SetOutput(output));

        var staticSite = output / "wwwroot";
        if (!Directory.Exists(staticSite))
        {
            Assert.Fail($"WASM publish finished but {staticSite} was not produced.");
        }

        File.WriteAllText(staticSite / "INSTALL.txt", """
            Jev WASM static site
            ====================
            Serve this folder from any static host (nginx, GitHub Pages, Azure Static Web Apps).
            The site is a Blazor WebAssembly host; coding-worker CLIs are not available in the browser.
            """);
        ZipDirectory(staticSite, WasmZip);
        Log.Information("WASM package: {Zip}", WasmZip);
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

        MakeExecutable(output / "Jev");
        File.WriteAllText(output / "INSTALL.txt", """
            Jev Linux desktop (Photino)
            ===========================
            Portable: extract and run ./Jev (needs WebKitGTK, e.g. libwebkit2gtk-4.1-0).
            Installer: sudo dpkg -i jev_<version>_amd64.deb && jev
            """);

        CreateTarball(output, LinuxTarball);
        CreateDebianPackage(output);
        Log.Information("Linux tarball: {Tar}", LinuxTarball);
        Log.Information("Linux installer: {Deb}", LinuxDeb);
    }

    void PublishWindowsPortableCore()
    {
        var output = ArtifactsDirectory / "windows" / "portable";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(LinuxProject)
            .SetConfiguration(Configuration.Release)
            .SetRuntime("win-x64")
            .SetSelfContained(true)
            .SetOutput(output));

        File.WriteAllText(output / "INSTALL.txt", """
            Jev Windows portable (Photino + WebView2)
            =========================================
            Unzip and run Jev.exe. Windows 10/11 already include WebView2 in most cases.
            This zip is produced on Linux CI. The MAUI MSIX installer still requires a Windows pack machine.
            """);
        ZipDirectory(output, WindowsPortableZip);
        Log.Information("Windows portable: {Zip}", WindowsPortableZip);
    }

    void PublishWindowsMauiCore()
    {
        RequireWindows("PublishWindows");
        RequireMauiWorkload("maui-windows", "PublishWindows");
        var output = ArtifactsDirectory / "windows" / "maui";
        PrepareDirectory(output);
        DotNetPublish(s => s
            .SetProject(MauiProject)
            .SetConfiguration(Configuration.Release)
            .SetFramework("net10.0-windows10.0.19041.0")
            .SetProperty("WindowsPackageType", "None")
            .SetOutput(output));
        var zip = ArtifactsDirectory / "jev-windows-maui-x64.zip";
        ZipDirectory(output, zip);
        Log.Information("Windows MAUI unpackaged zip: {Zip}", zip);
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

    void CreateDebianPackage(AbsolutePath appDir)
    {
        var staging = ArtifactsDirectory / "linux" / "deb";
        PrepareDirectory(staging);
        var opt = staging / "opt" / "jev";
        CopyDirectory(appDir, opt);
        MakeExecutable(opt / "Jev");

        Directory.CreateDirectory(staging / "usr" / "bin");
        File.WriteAllText(staging / "usr" / "bin" / "jev", """
            #!/bin/sh
            set -e
            cd /opt/jev
            exec /opt/jev/Jev "$@"
            """);
        MakeExecutable(staging / "usr" / "bin" / "jev");

        Directory.CreateDirectory(staging / "usr" / "share" / "applications");
        File.WriteAllText(staging / "usr" / "share" / "applications" / "jev.desktop", """
            [Desktop Entry]
            Name=Jev
            Comment=Strategy-backed coding assistant
            Exec=/usr/bin/jev
            Icon=jev
            Terminal=false
            Type=Application
            Categories=Development;
            """);

        if (File.Exists(IconSvg))
        {
            var icons = staging / "usr" / "share" / "icons" / "hicolor" / "scalable" / "apps";
            Directory.CreateDirectory(icons);
            File.Copy(IconSvg, icons / "jev.svg", overwrite: true);
        }

        var installedBytes = Directory.GetFiles(staging, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
        var debian = staging / "DEBIAN";
        Directory.CreateDirectory(debian);
        File.WriteAllText(debian / "control", $"""
            Package: jev
            Version: {Version}
            Section: devel
            Priority: optional
            Architecture: amd64
            Maintainer: Jev contributors <noreply@users.noreply.github.com>
            Installed-Size: {(installedBytes / 1024).ToString(CultureInfo.InvariantCulture)}
            Depends: libc6, libwebkit2gtk-4.1-0 | libwebkit2gtk-4.0-37
            Homepage: https://github.com/sharpninja/jev-codex-blazor
            Description: Jev coding-assistant Hybrid desktop
             Blazor Hybrid chat UI over strategy-pattern coding workers
             (Codex, Claude, Grok Build, Cline). Workers use subscription login.
            """);

        if (File.Exists(LinuxDeb))
        {
            File.Delete(LinuxDeb);
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dpkg-deb",
            Arguments = $"--build --root-owner-group \"{staging}\" \"{LinuxDeb}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Failed to start dpkg-deb.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Fail($"dpkg-deb exited {process.ExitCode}: {process.StandardError.ReadToEnd()}");
        }
    }

    void WriteChecksums()
    {
        var names = new[]
        {
            WasmZip.Name,
            LinuxTarball.Name,
            LinuxDeb.Name,
            WindowsPortableZip.Name
        };

        var lines = new List<string>();
        foreach (var name in names)
        {
            var path = ArtifactsDirectory / name;
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            lines.Add($"{hash}  {name}");
        }

        File.WriteAllLines(ChecksumsFile, lines);
        Log.Information("Checksums: {File}", ChecksumsFile);
    }

    void WriteReleaseNotes()
    {
        var notes = ArtifactsDirectory / "RELEASE_NOTES.md";
        File.WriteAllText(notes, $"""
            # Jev {Version}

            First packaged Hybrid release: shared `Jev.App` UI, strategy workers, subscription-login auth.

            ## Install

            **Linux (installer)**
            ```bash
            sudo dpkg -i jev_{Version}_amd64.deb
            jev
            ```
            Needs WebKitGTK (`libwebkit2gtk-4.1-0` on Ubuntu).

            **Linux (portable)**
            ```bash
            tar -xzf jev-linux-x64.tar.gz
            ./Jev
            ```

            **Windows (portable Photino)**
            Unzip `jev-windows-x64.zip` and run `Jev.exe` (WebView2). MAUI MSIX still requires a Windows pack machine (`./build.sh PublishWindows`).

            **WASM**
            Unzip `jev-wasm.zip` and serve the folder. Coding CLIs are not available in the browser.

            **Android / MAUI Windows**
            `./build.sh PublishAndroid` and `./build.sh PublishWindows` fail on this Linux pack host unless the MAUI + Android/Windows SDKs are installed.

            ## Checksums
            See `SHA256SUMS`.
            """);
    }

    void CreateGitHubRelease()
    {
        var notes = ArtifactsDirectory / "RELEASE_NOTES.md";
        var assets = new[] { WasmZip, LinuxTarball, LinuxDeb, WindowsPortableZip, ChecksumsFile }
            .Where(path => File.Exists(path))
            .Select(path => $"\"{path}\"")
            .ToArray();
        if (assets.Length == 0)
        {
            Assert.Fail("Release has no artifacts to upload.");
        }

        var arguments = new StringBuilder();
        arguments.Append($"release create {ReleaseTag} ");
        arguments.Append($"--title \"Jev {Version}\" ");
        arguments.Append($"--notes-file \"{notes}\" ");
        arguments.Append("--prerelease ");
        arguments.Append($"--target {GetCurrentCommit()} ");
        arguments.Append(string.Join(' ', assets));

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "gh",
            Arguments = arguments.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Failed to start gh.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Fail($"gh release create failed ({process.ExitCode}): {stderr}\n{stdout}");
        }

        Log.Information("GitHub release: {Output}", stdout.Trim());
    }

    static string GetCurrentCommit()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start git.");
        var sha = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return sha;
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

    static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination), overwrite: true);
        }
    }

    static void MakeExecutable(AbsolutePath path)
    {
        if (!File.Exists(path) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{path}\"",
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start chmod.");
        process.WaitForExit();
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
