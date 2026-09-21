using System.Diagnostics;
using System.Text;

namespace Jev.Codex.Tests;

public sealed class ProcessUtf8Tests
{
    public const string NonAsciiPrompt = "Who are you? — café";

    [Fact]
    public void ConfigureRedirects_sets_stdin_to_utf8_without_bom()
    {
        var start = new ProcessStartInfo();

        ProcessUtf8.ConfigureRedirects(start);

        Assert.True(start.RedirectStandardInput);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Same(ProcessUtf8.Utf8NoBom, start.StandardInputEncoding);
        Assert.Empty(ProcessUtf8.Utf8NoBom.GetPreamble());
        Assert.Equal(Encoding.UTF8.WebName, start.StandardOutputEncoding!.WebName);
        Assert.Equal(Encoding.UTF8.WebName, start.StandardErrorEncoding!.WebName);
    }

    [Fact]
    public async Task WriteStdinAsync_encodes_em_dash_and_cafe_as_utf8_without_bom()
    {
        using var stream = new MemoryStream();

        await ProcessUtf8.WriteStdinAsync(stream, NonAsciiPrompt, CancellationToken.None);

        var bytes = stream.ToArray();
        Assert.NotEmpty(bytes);
        Assert.False(HasUtf8Bom(bytes));
        var decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        Assert.Equal(NonAsciiPrompt, decoded);
        Assert.True(bytes.AsSpan().IndexOf("—"u8) >= 0);
        Assert.True(bytes.AsSpan().IndexOf("é"u8) >= 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task WriteStdinAsync_skips_empty_input(string? text)
    {
        using var stream = new MemoryStream();

        await ProcessUtf8.WriteStdinAsync(stream, text, CancellationToken.None);

        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task CodexProcessRunner_writes_valid_utf8_stdin_without_bom()
    {
        using var dir = new TempDir();
        var dump = Path.Combine(dir.Path, "stdin.bin");
        var runner = new CodexProcessRunner();
        var start = Utf8StdinStub.CreateDumpStartInfo(dir, dump);

        var result = await runner.RunAsync(start, NonAsciiPrompt, null, null, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Utf8StdinStub.AssertValidUtf8WithoutBom(dump, NonAsciiPrompt);
    }

    internal static bool HasUtf8Bom(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
}

internal static class Utf8StdinStub
{
    public static ProcessStartInfo CreateDumpStartInfo(TempDir dir, string outputPath)
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

    public static void AssertValidUtf8WithoutBom(string path, string expected)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.False(ProcessUtf8Tests.HasUtf8Bom(bytes));
        var decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        Assert.Equal(expected, decoded);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("jev-utf8-").FullName;

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
