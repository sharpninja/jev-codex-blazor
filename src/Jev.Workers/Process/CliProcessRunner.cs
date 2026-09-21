using System.Diagnostics;
using System.Text;
using Jev.Codex;

namespace Jev.Workers.Process;

public sealed class CliProcessRunner : ICliProcessRunner
{
    private readonly CliSearchEnvironment _environment;

    public CliProcessRunner() : this(CliSearchEnvironment.Current)
    {
    }

    public CliProcessRunner(CliSearchEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<CliProcessRunResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        IProgress<string>? stdoutLine,
        IProgress<string>? stderrLine,
        CancellationToken cancellationToken)
    {
        ProcessUtf8.ConfigureRedirects(startInfo);

        var requested = startInfo.FileName;
        if (!CliExecutableResolver.TryApply(startInfo, _environment))
        {
            throw new CliExecutableNotFoundException(requested);
        }

        System.Diagnostics.Process process;
        try
        {
            process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{requested}'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or PlatformNotSupportedException)
        {
            throw new CliExecutableNotFoundException(requested, ex);
        }

        using var _ = process;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = ReadLinesAsync(process.StandardOutput, stdout, stdoutLine, cancellationToken);
        var stderrTask = ReadLinesAsync(process.StandardError, stderr, stderrLine, cancellationToken);

        if (!string.IsNullOrEmpty(standardInput))
        {
            await ProcessUtf8.WriteStdinAsync(process.StandardInput.BaseStream, standardInput, cancellationToken);
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
}
