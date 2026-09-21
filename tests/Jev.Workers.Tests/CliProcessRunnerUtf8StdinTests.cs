using System.Diagnostics;
using System.Text;
using Jev.Workers.Process;

namespace Jev.Workers.Tests;

public sealed class CliProcessRunnerUtf8StdinTests
{
    [Fact]
    public async Task Writes_em_dash_and_cafe_as_valid_utf8_stdin_without_bom()
    {
        const string prompt = "Who are you? — café";
        using var dir = new Utf8TempDir();
        var dump = Path.Combine(dir.Path, "stdin.bin");
        var runner = new CliProcessRunner();
        var start = CreateDumpStartInfo(dir, dump);

        var result = await runner.RunAsync(start, prompt, null, null, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        var bytes = File.ReadAllBytes(dump);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        Assert.Equal(prompt, decoded);
        Assert.True(bytes.AsSpan().IndexOf("—"u8) >= 0);
        Assert.True(bytes.AsSpan().IndexOf("é"u8) >= 0);
    }

    private static ProcessStartInfo CreateDumpStartInfo(Utf8TempDir dir, string outputPath)
    {
        if (OperatingSystem.IsWindows())
        {
            var script = dir.Write("dump-stdin.ps1", """
                $out = $args[0]
                $fs = [System.IO.File]::Create($out)
                try {
                  [Console]::OpenStandardInput().CopyTo($fs)
                } finally {
                  $fs.Dispose()
                }
                """);
            return new ProcessStartInfo
            {
                FileName = "powershell",
                ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, outputPath }
            };
        }

        var unix = dir.WriteExecutable("dump-stdin", """
            #!/bin/sh
            cat > "$1"
            """);
        return new ProcessStartInfo
        {
            FileName = unix,
            ArgumentList = { outputPath }
        };
    }

    private sealed class Utf8TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("jev-cli-utf8-").FullName;

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
