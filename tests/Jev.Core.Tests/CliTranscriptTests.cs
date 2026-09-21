using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Jev.Workers;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class CliTranscriptTests
{
    [Fact]
    public void Append_maps_progress_channels_and_redacts_secrets()
    {
        var transcript = new CliTranscript();
        var changed = 0;
        transcript.Changed += (_, _) => changed++;

        transcript.Append(new CodingProgress(CodingProgress.Input, "codex exec OPENAI_API_KEY=sk-secretsecretsecret"));
        transcript.Append(new CodingProgress(CodingProgress.Stdout, "{\"type\":\"thread.started\"}"));
        transcript.Append(new CodingProgress(CodingProgress.Stderr, "warn"));
        transcript.Append(new CodingProgress(CodingProgress.Starting, "Starting Codex"));

        Assert.Equal(4, transcript.Lines.Count);
        Assert.Equal(4, changed);
        Assert.Equal(CliTranscriptChannel.Input, transcript.Lines[0].Channel);
        Assert.Equal(CliTranscriptChannel.Stdout, transcript.Lines[1].Channel);
        Assert.Equal(CliTranscriptChannel.Stderr, transcript.Lines[2].Channel);
        Assert.Equal(CliTranscriptChannel.System, transcript.Lines[3].Channel);
        Assert.Contains("[redacted]", transcript.Lines[0].Text);
        Assert.DoesNotContain("sk-secretsecretsecret", transcript.Lines[0].Text);
    }

    [Fact]
    public void Clear_empties_the_log_and_notifies()
    {
        var transcript = new CliTranscript();
        var changed = 0;
        transcript.Changed += (_, _) => changed++;
        transcript.Append(CliTranscriptChannel.Stdout, "hello");
        transcript.Clear();

        Assert.Empty(transcript.Lines);
        Assert.Equal(2, changed);
    }

    [Fact]
    public async Task Simulator_forwards_worker_progress_into_the_transcript()
    {
        var worker = new RecordingStrategy
        {
            ProgressToReport =
            [
                new CodingProgress(CodingProgress.Starting, "Starting Codex"),
                new CodingProgress(CodingProgress.Input, "codex exec --json -"),
                new CodingProgress(CodingProgress.Stdout, "line-one")
            ]
        };
        var transcript = new CliTranscript();
        var status = new JevRunStatus();
        var simulator = new JevCliSimulator(
            new JevPersona(Options.Create(new JevAgentOptions())),
            new FixedSelector(worker),
            status,
            transcript);

        await simulator.SendAsync("scaffold hello");

        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.Input && line.Text.Contains("codex"));
        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.Stdout && line.Text == "line-one");
        Assert.Contains(status.WorkerEvents, item => item.Phase == CodingProgress.Starting);
        Assert.DoesNotContain(status.WorkerEvents, item => item.Phase is CodingProgress.Input or CodingProgress.Stdout);
    }

    [Fact]
    public async Task Probe_on_an_unsupported_host_writes_the_gated_message()
    {
        var worker = new RecordingStrategy { Unsupported = true };
        var transcript = new CliTranscript();
        var simulator = new JevCliSimulator(
            new JevPersona(Options.Create(new JevAgentOptions())),
            new FixedSelector(worker),
            new JevRunStatus(),
            transcript);

        var result = await simulator.SendAsync("Is the coding worker available?");

        Assert.Contains("wasm", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(transcript.Lines, line => line.Channel == CliTranscriptChannel.System && line.Text.Contains("wasm"));
    }
}
