using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevCliSimulatorTests
{
    [Fact]
    public async Task Who_are_you_runs_the_selected_cli_with_jev_persona()
    {
        var worker = new RecordingStrategy();
        var status = new JevRunStatus();
        var transcript = new CliTranscript();
        var simulator = Create(worker, status, transcript);

        var result = await simulator.SendAsync("who are you?");

        Assert.True(result.Succeeded);
        Assert.Single(worker.Prompts);
        Assert.Contains("You are running as Jev", worker.Prompts[0], StringComparison.Ordinal);
        Assert.Contains("who are you?", worker.Prompts[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("persona", worker.Prompts[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback router", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orchestration LLM key", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("I'm Jev, simulated by Codex", result.Text, StringComparison.Ordinal);
        Assert.Equal("Codex CLI", status.Simulator);
        Assert.Equal(JevPhase.Done, status.Phase);
        Assert.Equal("thread-test", simulator.SessionId);
    }

    [Fact]
    public async Task Missing_worker_returns_probe_error_without_running_the_cli()
    {
        var worker = new RecordingStrategy { Missing = true };
        var simulator = Create(worker);

        var result = await simulator.SendAsync("who are you?");

        Assert.False(result.Succeeded);
        Assert.Empty(worker.Prompts);
        Assert.True(worker.Probed);
        Assert.Contains("not found", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback router", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Not_logged_in_worker_returns_login_error_without_running()
    {
        var worker = new RecordingStrategy { NotLoggedIn = true };
        var simulator = Create(worker);

        var result = await simulator.SendAsync("scaffold a hello console app");

        Assert.False(result.Succeeded);
        Assert.Empty(worker.Prompts);
        Assert.Contains("not logged in", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("codex login", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unsupported_host_returns_gated_error()
    {
        var worker = new RecordingStrategy { Unsupported = true };
        var transcript = new CliTranscript();
        var simulator = Create(worker, transcript: transcript);

        var result = await simulator.SendAsync("who are you?");

        Assert.False(result.Succeeded);
        Assert.Empty(worker.Prompts);
        Assert.Contains("wasm", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.System && line.Text.Contains("wasm"));
    }

    [Fact]
    public async Task Follow_up_includes_history_and_resumes_the_worker_session()
    {
        var worker = new RecordingStrategy();
        var simulator = Create(worker);

        await simulator.SendAsync("hello");
        var second = await simulator.SendAsync("create a file in this workspace");

        Assert.Equal(2, worker.Requests.Count);
        Assert.Null(worker.Requests[0].SessionId);
        Assert.Null(worker.Requests[0].WorkingDirectory);
        Assert.Equal("thread-test", worker.Requests[1].SessionId);
        Assert.Equal("/tmp/jev-test", worker.Requests[1].WorkingDirectory);
        Assert.Equal("/tmp/jev-test", simulator.Workspace);
        Assert.Contains("hello", worker.Prompts[1], StringComparison.Ordinal);
        Assert.Contains("create a file in this workspace", worker.Prompts[1], StringComparison.Ordinal);
        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task First_turn_lets_the_worker_choose_its_configured_workspace()
    {
        var worker = new RecordingStrategy { DefaultWorkspace = "/tmp/configured-project" };
        var simulator = Create(worker);

        await simulator.SendAsync("who are you?");

        Assert.Null(worker.Requests[0].WorkingDirectory);
        Assert.Equal("/tmp/configured-project", simulator.Workspace);
    }

    [Fact]
    public async Task Failed_worker_run_surfaces_error_and_stderr_not_only_the_final_message()
    {
        var worker = new RecordingStrategy
        {
            RunFailed = true,
            RunFinalMessage = "I started the scaffold.",
            RunError = "Codex exited 1. permission denied"
        };
        var simulator = Create(worker);

        var result = await simulator.SendAsync("scaffold a hello console app");

        Assert.False(result.Succeeded);
        Assert.Contains("permission denied", result.Text, StringComparison.Ordinal);
        Assert.Contains("did not complete successfully", result.Text, StringComparison.Ordinal);
        Assert.Contains("agent failed", result.Text, StringComparison.Ordinal);
        Assert.Contains("I started the scaffold.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forwards_worker_progress_into_the_transcript()
    {
        var worker = new RecordingStrategy
        {
            ProgressToReport =
            [
                new Jev.Workers.CodingProgress(Jev.Workers.CodingProgress.Starting, "Starting Codex"),
                new Jev.Workers.CodingProgress(Jev.Workers.CodingProgress.Input, "codex exec --json -"),
                new Jev.Workers.CodingProgress(Jev.Workers.CodingProgress.Stdout, "line-one")
            ]
        };
        var transcript = new CliTranscript();
        var status = new JevRunStatus();
        var simulator = Create(worker, status, transcript);

        await simulator.SendAsync("scaffold hello");

        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.Input && line.Text.Contains("codex"));
        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.Stdout && line.Text == "line-one");
        Assert.Contains(status.WorkerEvents, item => item.Phase == Jev.Workers.CodingProgress.Starting);
        Assert.DoesNotContain(status.WorkerEvents, item => item.Phase is Jev.Workers.CodingProgress.Input or Jev.Workers.CodingProgress.Stdout);
    }

    private static JevCliSimulator Create(
        RecordingStrategy worker,
        JevRunStatus? status = null,
        CliTranscript? transcript = null)
        => new(
            new JevPersona(Options.Create(new JevAgentOptions())),
            new FixedSelector(worker),
            status ?? new JevRunStatus(),
            transcript ?? new CliTranscript());
}
