using Jev.Core.Agent;
using Jev.Core.Tools;
using Microsoft.Extensions.AI;

namespace Jev.Core.Tests;

public sealed class FallbackJevChatClientTests
{
    [Fact]
    public async Task Emits_scaffold_function_call_for_demo_prompt()
    {
        using var client = new FallbackJevChatClient();
        var tools = new AITool[] { DummyFunction(JevCodexTools.ScaffoldHelloConsoleName) };

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Scaffold a hello console app in a temp workspace")],
            new ChatOptions { Tools = tools });

        var call = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Single();
        Assert.Equal(JevCodexTools.ScaffoldHelloConsoleName, call.Name);
    }

    [Fact]
    public async Task Summarizes_function_results_without_another_tool_call()
    {
        using var client = new FallbackJevChatClient();
        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "scaffold hello"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", JevCodexTools.ScaffoldHelloConsoleName)]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("1", "Codex CLI is not available")])
        ]);

        Assert.Contains("Codex CLI is not available", response.Text);
        Assert.Empty(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
    }

    [Fact]
    public async Task Follow_up_coding_ask_does_not_reuse_previous_tool_result()
    {
        using var client = new FallbackJevChatClient();
        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "Is Codex available?"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", JevCodexTools.ProbeName)]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("1", "Codex CLI is not available")]),
            new ChatMessage(ChatRole.Assistant, "I delegated that to Codex."),
            new ChatMessage(ChatRole.User, "Scaffold a hello console app in a temp workspace")
        ]);

        var call = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Single();
        Assert.Equal(JevCodexTools.ScaffoldHelloConsoleName, call.Name);
    }

    private static AIFunction DummyFunction(string name)
        => AIFunctionFactory.Create(() => "ok", name);
}
