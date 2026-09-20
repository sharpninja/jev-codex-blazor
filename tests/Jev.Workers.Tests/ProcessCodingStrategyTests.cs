using System.Diagnostics;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Tests;

public sealed class ProcessCodingStrategyTests
{
    [Fact]
    public async Task Restricted_host_does_not_spawn_a_cli()
    {
        var runner = new ScriptedRunner(_ => throw new InvalidOperationException("CLI should not run on WASM/Android"));
        var strategy = new ClaudeCodingStrategy(
            Options.Create(new ClaudeCliOptions { ExecutablePath = "claude" }),
            runner,
            NullLogger<ClaudeCodingStrategy>.Instance,
            CodingHost.Restricted("wasm"));

        var availability = await strategy.ProbeAsync();
        Assert.False(availability.IsSupported);
        Assert.False(availability.IsInstalled);
        Assert.Contains("wasm", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);

        var progress = new List<CodingProgress>();
        var result = await strategy.RunAsync(
            new CodingTaskRequest { Prompt = "hi" },
            new ImmediateProgress<CodingProgress>(progress.Add));
        Assert.False(result.Succeeded);
        Assert.Equal(126, result.ExitCode);
        Assert.Null(runner.LastStartInfo);
        Assert.Contains(progress, item => item.Phase == CodingProgress.System && item.Message.Contains("wasm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Claude_probe_reports_missing_binary()
    {
        var runner = new ScriptedRunner(_ => throw new CliExecutableNotFoundException("claude"));
        var strategy = new ClaudeCodingStrategy(
            Options.Create(new ClaudeCliOptions { ExecutablePath = "claude" }),
            runner,
            NullLogger<ClaudeCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();
        Assert.False(availability.IsInstalled);
        Assert.Equal(CodingStrategyKind.Claude, availability.Kind);
        Assert.Contains("claude", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("claude auth login", availability.LoginCommand);
    }

    [Fact]
    public async Task Claude_probe_separates_installed_from_not_logged_in()
    {
        var runner = new ScriptedRunner(start =>
        {
            if (start.ArgumentList.Contains("status"))
            {
                return new CliProcessRunResult { ExitCode = 1, Stderr = "not logged in" };
            }

            return new CliProcessRunResult { ExitCode = 0, Stdout = "2.1.0" };
        });
        var strategy = new ClaudeCodingStrategy(
            Options.Create(new ClaudeCliOptions { ExecutablePath = "claude" }),
            runner,
            NullLogger<ClaudeCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();
        Assert.True(availability.IsInstalled);
        Assert.False(availability.IsLoggedIn);
        Assert.False(availability.IsReady);
        Assert.Contains("claude auth login", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", availability.FormatForAgent(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrokBuild_probe_is_installed_without_inventing_a_status_command()
    {
        var runner = new ScriptedRunner(_ => new CliProcessRunResult { ExitCode = 0, Stdout = "grok 0.1" });
        var strategy = new GrokBuildCodingStrategy(
            Options.Create(new GrokBuildCliOptions { ExecutablePath = "grok" }),
            runner,
            NullLogger<GrokBuildCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();
        Assert.True(availability.IsInstalled);
        Assert.Null(availability.IsLoggedIn);
        Assert.Equal("grok login", availability.LoginCommand);
        Assert.Contains("grok login", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("--version", string.Join(' ', runner.LastStartInfo!.ArgumentList));
    }

    [Fact]
    public async Task GrokBuild_run_captures_command_and_json_result()
    {
        var runner = new ScriptedRunner(_ => new CliProcessRunResult
        {
            ExitCode = 0,
            Stdout = """{"result":"Created Program.cs","session_id":"g-9"}"""
        });
        var strategy = new GrokBuildCodingStrategy(
            Options.Create(new GrokBuildCliOptions { ExecutablePath = "grok" }),
            runner,
            NullLogger<GrokBuildCodingStrategy>.Instance);

        var result = await strategy.RunAsync(new CodingTaskRequest
        {
            Prompt = "scaffold hello",
            WorkingDirectory = Path.GetTempPath()
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Created Program.cs", result.FinalMessage);
        Assert.Equal("g-9", result.SessionId);
        Assert.Contains("grok", result.CommandLine);
        Assert.Contains("-p", runner.LastStartInfo!.ArgumentList);
        Assert.Contains("--always-approve", runner.LastStartInfo.ArgumentList);
    }

    [Fact]
    public async Task GrokBuild_run_streams_sanitized_input_and_output_as_they_arrive()
    {
        var runner = new ScriptedRunner(_ => new CliProcessRunResult
        {
            ExitCode = 0,
            Stdout = "line-one\n{\"result\":\"ok\"}\n",
            Stderr = "warn-one\n"
        });
        var strategy = new GrokBuildCodingStrategy(
            Options.Create(new GrokBuildCliOptions { ExecutablePath = "grok" }),
            runner,
            NullLogger<GrokBuildCodingStrategy>.Instance);

        var progress = new List<CodingProgress>();
        var result = await strategy.RunAsync(
            new CodingTaskRequest
            {
                Prompt = "use OPENAI_API_KEY=sk-secretsecretsecret",
                WorkingDirectory = Path.GetTempPath()
            },
            new ImmediateProgress<CodingProgress>(progress.Add));

        Assert.True(result.Succeeded);
        Assert.Contains(progress, item => item.Phase == CodingProgress.Starting);
        Assert.Contains(progress, item => item.Phase == CodingProgress.Input && item.Message.Contains("grok"));
        Assert.Contains(progress, item => item.Phase == CodingProgress.Input && item.Message.Contains(SecretSanitizer.Redacted));
        Assert.DoesNotContain(progress, item => item.Message.Contains("sk-secretsecretsecret", StringComparison.Ordinal));
        Assert.Contains(progress, item => item.Phase == CodingProgress.Stdout && item.Message == "line-one");
        Assert.Contains(progress, item => item.Phase == CodingProgress.Stdout && item.Message.Contains("\"result\""));
        Assert.Contains(progress, item => item.Phase == CodingProgress.Stderr && item.Message == "warn-one");
        Assert.Equal(2, runner.StdoutCallbacks);
        Assert.Equal(1, runner.StderrCallbacks);
    }

    [Fact]
    public async Task Cline_probe_uses_version_then_falls_back_to_dashed_version()
    {
        var calls = new List<string>();
        var runner = new ScriptedRunner(start =>
        {
            var args = string.Join(' ', start.ArgumentList);
            calls.Add(args);
            if (args == "version")
            {
                return new CliProcessRunResult { ExitCode = 1, Stderr = "unknown option" };
            }

            if (args == "--version")
            {
                return new CliProcessRunResult { ExitCode = 0, Stdout = "cline 1.2.3" };
            }

            return new CliProcessRunResult { ExitCode = 1 };
        });
        var strategy = new ClineCodingStrategy(
            Options.Create(new ClineCliOptions { ExecutablePath = "cline" }),
            runner,
            NullLogger<ClineCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();

        Assert.True(availability.IsInstalled);
        Assert.True(availability.IsReady);
        Assert.Contains("1.2.3", availability.Version);
        Assert.Equal(["version", "--version"], calls);
        Assert.DoesNotContain("not installed", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cline_probe_does_not_report_missing_when_version_exits_nonzero()
    {
        var runner = new ScriptedRunner(_ => new CliProcessRunResult { ExitCode = 2, Stderr = "usage: cline" });
        var strategy = new ClineCodingStrategy(
            Options.Create(new ClineCliOptions { ExecutablePath = "cline" }),
            runner,
            NullLogger<ClineCodingStrategy>.Instance);

        var availability = await strategy.ProbeAsync();

        Assert.True(availability.IsInstalled);
        Assert.False(availability.IsReady);
        Assert.Contains("exited 2", availability.FormatForAgent(), StringComparison.Ordinal);
        Assert.Contains("probe command failed", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not installed", availability.FormatForAgent(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cline_run_degrades_when_binary_missing()
    {
        var runner = new ScriptedRunner(_ => throw new CliExecutableNotFoundException("cline"));
        var strategy = new ClineCodingStrategy(
            Options.Create(new ClineCliOptions { ExecutablePath = "cline" }),
            runner,
            NullLogger<ClineCodingStrategy>.Instance);

        var result = await strategy.RunAsync(new CodingTaskRequest
        {
            Prompt = "hello",
            WorkingDirectory = Path.GetTempPath()
        });

        Assert.False(result.Succeeded);
        Assert.Equal(127, result.ExitCode);
        Assert.Contains("cline", result.CommandLine);
        Assert.Contains("--yolo", result.CommandLine);
    }

    [Fact]
    public async Task Claude_run_maps_auth_failure_to_subscription_login()
    {
        var runner = new ScriptedRunner(_ => new CliProcessRunResult
        {
            ExitCode = 1,
            Stderr = "Error: please sign in"
        });
        var strategy = new ClaudeCodingStrategy(
            Options.Create(new ClaudeCliOptions { ExecutablePath = "claude" }),
            runner,
            NullLogger<ClaudeCodingStrategy>.Instance);

        var result = await strategy.RunAsync(new CodingTaskRequest
        {
            Prompt = "hello",
            WorkingDirectory = Path.GetTempPath()
        });

        Assert.False(result.Succeeded);
        Assert.Contains("claude auth login", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--bare", result.CommandLine);
    }

    private sealed class ScriptedRunner : ICliProcessRunner
    {
        private readonly Func<ProcessStartInfo, CliProcessRunResult> _handler;

        public ScriptedRunner(Func<ProcessStartInfo, CliProcessRunResult> handler) => _handler = handler;

        public ProcessStartInfo? LastStartInfo { get; private set; }

        public int StdoutCallbacks { get; private set; }

        public int StderrCallbacks { get; private set; }

        public Task<CliProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            string? standardInput,
            IProgress<string>? stdoutLine,
            IProgress<string>? stderrLine,
            CancellationToken cancellationToken)
        {
            LastStartInfo = startInfo;
            var result = _handler(startInfo);
            foreach (var line in SplitLines(result.Stdout))
            {
                StdoutCallbacks++;
                stdoutLine?.Report(line);
            }

            foreach (var line in SplitLines(result.Stderr))
            {
                StderrCallbacks++;
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
