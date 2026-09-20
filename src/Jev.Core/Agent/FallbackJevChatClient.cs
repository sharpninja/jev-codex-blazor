using System.Runtime.CompilerServices;
using Jev.Core.Routing;
using Microsoft.Extensions.AI;

namespace Jev.Core.Agent;

/// <summary>
/// Local IChatClient used when no OpenAI key is configured. It still participates
/// in Microsoft Agent Framework: ChatClientAgent + FunctionInvokingChatClient
/// invoke Jev's Codex tools from the function calls this client emits.
/// </summary>
public sealed class FallbackJevChatClient : IChatClient
{
    private readonly ChatClientMetadata _metadata = new("jev-fallback", defaultModelId: "jev-router");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialized = messages.ToList();
        var lastUserIndex = materialized.FindLastIndex(message => message.Role == ChatRole.User);
        var resultAfterLatestUser = lastUserIndex >= 0
            ? materialized
                .Skip(lastUserIndex + 1)
                .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
                .LastOrDefault()
            : null;

        if (resultAfterLatestUser is not null)
        {
            return Task.FromResult(TextResponse(SummarizeToolResult(resultAfterLatestUser)));
        }

        var userText = lastUserIndex >= 0 ? materialized[lastUserIndex].Text?.Trim() ?? "" : "";
        var choice = JevIntentRouter.Choose(userText);

        return Task.FromResult(choice switch
        {
            JevToolChoice.ProbeCodex
                => FunctionCall(Jev.Core.Tools.JevCodexTools.ProbeName),
            JevToolChoice.ScaffoldHelloConsole
                => FunctionCall(Jev.Core.Tools.JevCodexTools.ScaffoldHelloConsoleName),
            JevToolChoice.RunCodingTask
                => FunctionCall(
                    Jev.Core.Tools.JevCodexTools.RunCodingTaskName,
                    new Dictionary<string, object?> { ["prompt"] = userText }),
            _ => TextResponse(ReplyWithoutTool(userText, choice))
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Contents);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType == typeof(ChatClientMetadata))
        {
            return _metadata;
        }

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private static ChatResponse FunctionCall(string name, IDictionary<string, object?>? arguments = null)
    {
        var call = new FunctionCallContent(Guid.NewGuid().ToString("n")[..8], name, arguments);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));
    }

    private static ChatResponse TextResponse(string text)
        => new(new ChatMessage(ChatRole.Assistant, text));

    private static string SummarizeToolResult(FunctionResultContent result)
    {
        var body = result.Result?.ToString();
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Codex returned an empty result. I will not invent what happened.";
        }

        var suffix = body.Contains("not found", StringComparison.OrdinalIgnoreCase)
                     || body.Contains("did not complete", StringComparison.OrdinalIgnoreCase)
                     || body.Contains("not available", StringComparison.OrdinalIgnoreCase)
            ? """

              If Codex is not installed, install the Codex CLI, put it on PATH, and retry. I will not pretend the files were written.
              """
            : "";

        return $"""
            I delegated that to Codex and here is the structured result.

            {body}
            {suffix}
            """;
    }

    private static string ReplyWithoutTool(string userText, JevToolChoice choice)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return "I'm Jev. Ask a coding question, or try the demo: scaffold a hello console app in a temp workspace.";
        }

        if (userText.Contains("who are you", StringComparison.OrdinalIgnoreCase)
            || userText.Contains("what are you", StringComparison.OrdinalIgnoreCase))
        {
            return """
                I'm Jev — a coding-assistant emulation layer. I plan and explain; Codex CLI does the file and tool work.
                This turn is using the local fallback router because no OpenAI API key is configured for Agent Framework orchestration.
                """;
        }

        if (choice is JevToolChoice.None)
        {
            return """
                I'm Jev. I can talk through a plan here, and I will send implementation work to Codex.
                Try "Is Codex available?" or "Scaffold a hello console app in a temp workspace."
                """;
        }

        return "I would call a Codex tool for that, but the tool is not registered on this agent.";
    }
}
