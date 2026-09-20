using System.Diagnostics;
using Jev.Codex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Codex.Tests;

public sealed class CodexCliClientTests
{
    [Fact]
    public async Task ExecAsync_parses_json_and_reports_progress()
    {
        var runner = new ScriptedProcessRunner(
            new ProcessRunResult
            {
                ExitCode = 0,
                Stdout = """
                    {"type":"thread.started","thread_id":"t-1"}
                    {"type":"item.completed","item":{"type":"file_change","changes":[{"path":"Program.cs"}]}}
                    {"type":"item.completed","item":{"type":"agent_message","text":"Scaffolded hello console"}}
                    """
            });

        var client = CreateClient(runner);
        var progress = new List<CodexProgress>();

        var result = await client.ExecAsync(
            new CodexExecRequest { Prompt = "scaffold hello", WorkingDirectory = Path.GetTempPath() },
            new Progress<CodexProgress>(progress.Add));

        Assert.True(result.Succeeded);
        Assert.Equal("t-1", result.ThreadId);
        Assert.Equal("Scaffolded hello console", result.FinalMessage);
        Assert.Contains("Program.cs", result.ChangedFiles);
        Assert.Contains("exec", runner.LastStartInfo!.ArgumentList);
        Assert.Equal("-", runner.LastStartInfo.ArgumentList[^1]);
        Assert.Equal("scaffold hello", runner.LastStdin);
        Assert.NotEmpty(progress);
        Assert.Contains(progress, item => item.Phase == "input" && item.Message.Contains("codex"));
        Assert.Contains(progress, item => item.Phase == "input" && item.Message.StartsWith("stdin:", StringComparison.Ordinal));
        Assert.Contains(progress, item => item.Phase == "stdout" && item.Message.Contains("thread.started"));
        Assert.Contains("codex", result.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecAsync_streams_unparsed_stdout_and_stderr()
    {
        var runner = new ScriptedProcessRunner(
            new ProcessRunResult
            {
                ExitCode = 0,
                Stdout = """
                    not-json
                    {"type":"thread.started","thread_id":"t-2"}
                    """,
                Stderr = "diag-one\n"
            });

        var progress = new List<CodexProgress>();
        var result = await CreateClient(runner).ExecAsync(
            new CodexExecRequest { Prompt = "hi", WorkingDirectory = Path.GetTempPath() },
            new Progress<CodexProgress>(progress.Add));

        Assert.True(result.Succeeded);
        Assert.Contains(progress, item => item.Phase == "stdout" && item.Message == "not-json");
        Assert.Contains(progress, item => item.Phase == "stderr" && item.Message == "diag-one");
        Assert.Equal("t-2", result.ThreadId);
    }

    [Fact]
    public async Task ExecAsync_degrades_when_binary_is_missing()
    {
        var runner = new ScriptedProcessRunner(_ => throw new CodexNotInstalledException("codex"));
        var client = CreateClient(runner);

        var result = await client.ExecAsync(new CodexExecRequest
        {
            Prompt = "hi",
            WorkingDirectory = Path.GetTempPath()
        });

        Assert.False(result.Succeeded);
        Assert.Equal(127, result.ExitCode);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_reports_missing_cli()
    {
        var runner = new ScriptedProcessRunner(_ => throw new CodexNotInstalledException("codex"));
        var availability = await CreateClient(runner).ProbeAsync();
        Assert.False(availability.IsInstalled);
        Assert.Contains("not found", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("codex login", availability.LoginCommand);
    }

    [Fact]
    public async Task ProbeAsync_reports_installed_but_not_logged_in()
    {
        var runner = new ScriptedProcessRunner(start =>
        {
            if (start.ArgumentList.Contains("status"))
            {
                return new ProcessRunResult { ExitCode = 1, Stderr = "Not logged in" };
            }

            return new ProcessRunResult { ExitCode = 0, Stdout = "codex-cli 0.50.0" };
        });

        var availability = await CreateClient(runner).ProbeAsync();
        Assert.True(availability.IsInstalled);
        Assert.False(availability.IsLoggedIn);
        Assert.Contains("codex login", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ChatGPT", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
    }

    private static CodexCliClient CreateClient(ICodexProcessRunner runner)
        => new(
            Options.Create(new CodexCliOptions { ExecutablePath = "codex" }),
            runner,
            NullLogger<CodexCliClient>.Instance);

    private sealed class ScriptedProcessRunner : ICodexProcessRunner
    {
        private readonly Func<ProcessStartInfo, ProcessRunResult> _handler;

        public ScriptedProcessRunner(ProcessRunResult result) : this(_ => result)
        {
        }

        public ScriptedProcessRunner(Func<ProcessStartInfo, ProcessRunResult> handler)
        {
            _handler = handler;
        }

        public ProcessStartInfo? LastStartInfo { get; private set; }

        public string? LastStdin { get; private set; }

        public Task<ProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            string? standardInput,
            IProgress<string>? stdoutLine,
            IProgress<string>? stderrLine,
            CancellationToken cancellationToken)
        {
            LastStartInfo = startInfo;
            LastStdin = standardInput;
            var result = _handler(startInfo);
            foreach (var line in SplitLines(result.Stdout))
            {
                stdoutLine?.Report(line);
            }

            foreach (var line in SplitLines(result.Stderr))
            {
                stderrLine?.Report(line);
            }

            return Task.FromResult(result);
        }

        private static IEnumerable<string> SplitLines(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            if (normalized.EndsWith('\n'))
            {
                normalized = normalized[..^1];
            }

            foreach (var line in normalized.Split('\n'))
            {
                yield return line;
            }
        }
    }
}
