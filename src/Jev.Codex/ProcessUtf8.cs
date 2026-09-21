using System.Diagnostics;
using System.Text;

namespace Jev.Codex;

/// <summary>
/// UTF-8 (no BOM) I/O for redirected CLI processes. On Windows, a redirected
/// stdin stream otherwise uses the system ANSI code page, which turns persona
/// punctuation such as em dashes into invalid UTF-8 for Codex.
/// </summary>
public static class ProcessUtf8
{
    public static UTF8Encoding Utf8NoBom { get; } = new(encoderShouldEmitUTF8Identifier: false);

    public static void ConfigureRedirects(ProcessStartInfo startInfo)
    {
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardInputEncoding = Utf8NoBom;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
    }

    public static async Task WriteStdinAsync(Stream stdin, string? text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var bytes = Utf8NoBom.GetBytes(text);
        await stdin.WriteAsync(bytes.AsMemory(), cancellationToken);
        await stdin.FlushAsync(cancellationToken);
    }
}
