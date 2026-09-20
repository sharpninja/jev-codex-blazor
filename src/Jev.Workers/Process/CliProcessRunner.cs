using System.Diagnostics;
using System.Text;

namespace Jev.Workers.Process;

public sealed class CliProcessRunner : ICliProcessRunner
{
    public async Task<CliProcessRunResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        IProgress<string>? stdoutLine,
        CancellationToken cancellationToken)
    {
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        System.Diagnostics.Process process;
        try
        {
            process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or PlatformNotSupportedException)
        {
            throw new CliExecutableNotFoundException(startInfo.FileName, ex);
        }

        using var _ = process;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = ReadLinesAsync(process.StandardOutput, stdout, stdoutLine, cancellationToken);
        var stderrTask = ReadToEndAsync(process.StandardError, stderr, cancellationToken);

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        return new CliProcessRunResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString()
        };
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder buffer,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            buffer.AppendLine(line);
            progress?.Report(line);
        }
    }

    private static async Task ReadToEndAsync(StreamReader reader, StringBuilder buffer, CancellationToken cancellationToken)
        => buffer.Append(await reader.ReadToEndAsync(cancellationToken));
}
