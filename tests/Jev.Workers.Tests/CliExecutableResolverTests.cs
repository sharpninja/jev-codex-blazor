using System.Diagnostics;
using Jev.Codex;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Tests;

public sealed class CliExecutableResolverTests
{
    [Fact]
    public void Resolves_windows_cmd_shim_via_PATHEXT()
    {
        using var dir = new TempDir();
        var shim = dir.Write("cline.cmd", "@echo off\r\necho cline 1.0\r\n");
        var env = IsolatedWindows(dir.Path, ".EXE;.CMD;.BAT;.PS1");

        var resolved = CliExecutableResolver.TryResolve("cline", env);

        Assert.NotNull(resolved);
        Assert.Equal(shim, resolved.Path, ignoreCase: true);
        Assert.Equal(CliLaunchKind.WindowsCmdScript, resolved.Kind);
        Assert.EndsWith("cmd.exe", resolved.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prefers_cmd_shim_over_extensionless_bash_script_on_windows()
    {
        using var dir = new TempDir();
        dir.Write("cline", "#!/bin/sh\necho bash-shim\n");
        var cmd = dir.Write("cline.cmd", "@echo off\r\n");
        var env = IsolatedWindows(dir.Path, ".COM;.EXE;.BAT;.CMD");

        var resolved = CliExecutableResolver.TryResolve("cline", env);

        Assert.NotNull(resolved);
        Assert.Equal(cmd, resolved.Path, ignoreCase: true);
        Assert.Equal(CliLaunchKind.WindowsCmdScript, resolved.Kind);
    }

    [Fact]
    public void Resolves_ps1_shim_via_PATHEXT()
    {
        using var dir = new TempDir();
        var shim = dir.Write("grok.ps1", "Write-Output 1");
        var env = IsolatedWindows(dir.Path, ".EXE;.PS1");

        var resolved = CliExecutableResolver.TryResolve("grok", env);

        Assert.NotNull(resolved);
        Assert.Equal(shim, resolved.Path, ignoreCase: true);
        Assert.Equal(CliLaunchKind.WindowsPowerShellScript, resolved.Kind);
    }

    [Fact]
    public void Resolves_extensionless_unix_binary_on_PATH()
    {
        using var dir = new TempDir();
        var shim = dir.WriteExecutable("claude", "#!/bin/sh\necho 2.1.0\n");
        var env = IsolatedUnix(dir.Path);

        var resolved = CliExecutableResolver.TryResolve("claude", env);

        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(shim), resolved.Path);
        Assert.Equal(CliLaunchKind.Native, resolved.Kind);
    }

    [Fact]
    public void Searches_npm_style_extra_directory_when_not_on_PATH()
    {
        using var pathDir = new TempDir();
        using var npmDir = new TempDir();
        var shim = npmDir.Write("cline", "#!/bin/sh\necho from-npm\n");
        var env = IsolatedUnix(pathDir.Path) with { ExtraDirectories = [npmDir.Path] };

        var resolved = CliExecutableResolver.TryResolve("cline", env);

        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(shim), resolved.Path);
    }

    [Fact]
    public void Prefers_explicit_absolute_path()
    {
        using var onPath = new TempDir();
        using var explicitDir = new TempDir();
        onPath.WriteExecutable("codex", "#!/bin/sh\necho path\n");
        var explicitShim = explicitDir.WriteExecutable("codex", "#!/bin/sh\necho explicit\n");
        var env = IsolatedUnix(onPath.Path);

        var resolved = CliExecutableResolver.TryResolve(explicitShim, env);

        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(explicitShim), resolved.Path);
    }

    [Fact]
    public void Returns_null_when_missing()
    {
        using var dir = new TempDir();
        var env = IsolatedUnix(dir.Path);

        Assert.Null(CliExecutableResolver.TryResolve("cline", env));
        Assert.Null(CliExecutableResolver.TryResolve(" ", env));
    }

    [Fact]
    public void ApplyTo_wraps_windows_cmd_shim_for_CreateProcess()
    {
        using var dir = new TempDir();
        var shim = dir.Write("cline.cmd", "@echo off\r\n");
        var resolved = CliExecutableResolver.TryResolve("cline", IsolatedWindows(dir.Path, ".CMD"));
        Assert.NotNull(resolved);

        var start = new ProcessStartInfo { FileName = "cline" };
        start.ArgumentList.Add("version");
        CliExecutableResolver.ApplyTo(start, resolved);

        Assert.EndsWith("cmd.exe", start.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/d", start.ArgumentList[0]);
        Assert.Equal("/s", start.ArgumentList[1]);
        Assert.Equal("/c", start.ArgumentList[2]);
        Assert.Contains(shim, start.ArgumentList[3], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version", start.ArgumentList[3], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_runner_starts_PATH_shim_the_way_a_terminal_would()
    {
        using var dir = new TempDir();
        CliSearchEnvironment env;
        if (OperatingSystem.IsWindows())
        {
            dir.Write(
                "cline.cmd",
                "@echo off\r\nif \"%~1\"==\"version\" goto version\r\n"
                + "if \"%~1\"==\"--version\" goto version\r\n"
                + "if \"%~1\"==\"-V\" goto version\r\n"
                + "echo unexpected: %*\r\nexit /b 1\r\n"
                + ":version\r\necho cline 3.1.0\r\nexit /b 0\r\n");
            env = IsolatedWindows(dir.Path, ".CMD");
        }
        else
        {
            dir.WriteExecutable(
                "cline",
                """
                #!/bin/sh
                if [ "$1" = version ] || [ "$1" = --version ] || [ "$1" = -V ]; then
                  echo "cline 3.1.0"
                  exit 0
                fi
                echo "unexpected: $*"
                exit 1
                """);
            env = IsolatedUnix(dir.Path);
        }
        var runner = new CliProcessRunner(env);
        var strategy = new ClineCodingStrategy(
            Options.Create(new ClineCliOptions { ExecutablePath = "cline" }),
            runner,
            NullLogger<ClineCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();

        Assert.True(availability.IsInstalled);
        Assert.True(availability.IsReady);
        Assert.Contains("3.1.0", availability.Version, StringComparison.Ordinal);
        Assert.Contains("cline", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not installed", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Real_runner_reports_not_found_without_pretending_the_process_started()
    {
        using var dir = new TempDir();
        var runner = new CliProcessRunner(IsolatedUnix(dir.Path));
        var start = new ProcessStartInfo { FileName = "cline" };

        var ex = await Assert.ThrowsAsync<CliExecutableNotFoundException>(
            () => runner.RunAsync(start, null, null, null, CancellationToken.None));

        Assert.Contains("cline", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PATHEXT", ex.Message, StringComparison.Ordinal);
        Assert.Contains("npm", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CliSearchEnvironment IsolatedUnix(string path)
        => new()
        {
            Path = path,
            IsWindows = false,
            IncludeWellKnownDirectories = false,
            QueryNpmPrefix = false
        };

    private static CliSearchEnvironment IsolatedWindows(string path, string pathExt)
        => new()
        {
            Path = path,
            PathExt = pathExt,
            IsWindows = true,
            IncludeWellKnownDirectories = false,
            QueryNpmPrefix = false
        };

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("jev-cli-").FullName;

        public string Write(string name, string contents)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, contents);
            return file;
        }

        public string WriteExecutable(string name, string contents)
        {
            var file = Write(name, contents);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    file,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            return file;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
