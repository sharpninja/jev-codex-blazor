using System.Diagnostics;
using System.Text;

namespace Jev.Codex;

public sealed class CodexProcessRunner : ICodexProcessRunner
{
    private readonly CliSearchEnvironment _environment;

    public CodexProcessRunner() : this(CliSearchEnvironment.Current)
    {
    }

    public CodexProcessRunner(CliSearchEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ProcessRunResult> RunAsync(
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
            throw new CodexNotInstalledException(requested);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{requested}'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or PlatformNotSupportedException)
        {
            throw new CodexNotInstalledException(requested, ex);
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

        return new ProcessRunResult
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
