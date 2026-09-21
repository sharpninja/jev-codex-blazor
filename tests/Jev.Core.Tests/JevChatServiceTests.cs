using Jev.App.Chat;
using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Jev.Workers;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevChatServiceTests
{
    [Fact]
    public async Task Send_who_are_you_uses_the_cli_not_a_fallback_router()
    {
        var worker = new RecordingStrategy();
        var status = new JevRunStatus();
        var transcript = new CliTranscript();
        var selector = new FixedSelector(worker);
        var simulator = new JevCliSimulator(
            new JevPersona(Options.Create(new JevAgentOptions())),
            selector,
            status,
            transcript);
        var chat = new JevChatService(
            simulator,
            status,
            selector,
            CodingHost.Desktop("server"),
            transcript);

        await chat.SendAsync("who are you?", () => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(2, chat.Turns.Count);
        Assert.Equal("user", chat.Turns[0].Role);
        Assert.Equal("jev", chat.Turns[1].Role);
        Assert.False(chat.Turns[1].IsError);
        Assert.Contains("I'm Jev, simulated by Codex", chat.Turns[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback router", chat.Turns[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orchestration LLM key", chat.Turns[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Codex CLI", chat.Simulator);
        Assert.Single(worker.Prompts);
        Assert.Contains("who are you?", worker.Prompts[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_without_an_installed_cli_surfaces_the_worker_error()
    {
        var worker = new RecordingStrategy { Missing = true };
        var status = new JevRunStatus();
        var transcript = new CliTranscript();
        var selector = new FixedSelector(worker);
        var chat = new JevChatService(
            new JevCliSimulator(
                new JevPersona(Options.Create(new JevAgentOptions())),
                selector,
                status,
                transcript),
            status,
            selector,
            CodingHost.Restricted("wasm"),
            transcript);

        await chat.SendAsync("who are you?", () => Task.CompletedTask, CancellationToken.None);

        Assert.True(chat.Turns[1].IsError);
        Assert.Contains("not found", chat.Turns[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback router", chat.Turns[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(worker.Prompts);
        Assert.Equal(JevPhase.Error, status.Phase);
    }
}
