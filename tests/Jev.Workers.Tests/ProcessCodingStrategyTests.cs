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

    private sealed class ScriptedRunner : ICliProcessRunner
    {
        private readonly Func<ProcessStartInfo, CliProcessRunResult> _handler;

        public ScriptedRunner(Func<ProcessStartInfo, CliProcessRunResult> handler) => _handler = handler;

        public ProcessStartInfo? LastStartInfo { get; private set; }

        public Task<CliProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            string? standardInput,
            IProgress<string>? stdoutLine,
            CancellationToken cancellationToken)
        {
            LastStartInfo = startInfo;
            return Task.FromResult(_handler(startInfo));
        }
    }
}
