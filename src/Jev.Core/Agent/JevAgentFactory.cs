using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Jev.Core.Agent;

public sealed class JevAgentFactory(
    JevPersona persona,
    JevCodexTools tools,
    IJevRunStatus status,
    IOptions<JevAgentOptions> jevOptions,
    IOptions<OpenAIOptions> openAiOptions,
    ILoggerFactory loggerFactory)
{
    public JevAgentHandle Create()
    {
        var openAi = openAiOptions.Value;
        var jev = jevOptions.Value;
        var apiKey = FirstNonEmpty(openAi.ApiKey, Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var model = FirstNonEmpty(openAi.Model, Environment.GetEnvironmentVariable("OPENAI_MODEL"), jev.Model)
                    ?? "gpt-4o-mini";
        var useFallback = jev.UseFallbackChatClient || string.IsNullOrWhiteSpace(apiKey);

        IChatClient chatClient = useFallback
            ? new FallbackJevChatClient()
            : new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();

        var agentTools = new AITool[]
        {
            AIFunctionFactory.Create(tools.ProbeCodexAsync, JevCodexTools.ProbeName),
            AIFunctionFactory.Create(tools.RunCodingTaskAsync, JevCodexTools.RunCodingTaskName),
            AIFunctionFactory.Create(tools.ScaffoldHelloConsoleAsync, JevCodexTools.ScaffoldHelloConsoleName)
        };

        AIAgent agent = chatClient.AsAIAgent(
            instructions: persona.LoadInstructions(),
            name: jev.Name,
            description: "Jev coding-assistant emulation layer over the Codex CLI.",
            tools: agentTools,
            loggerFactory: loggerFactory);

        var orchestration = useFallback
            ? "fallback-router"
            : $"openai:{model}";
        status.SetOrchestration(orchestration);

        return new JevAgentHandle(agent, orchestration, useFallback);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public sealed record JevAgentHandle(AIAgent Agent, string Orchestration, bool UsesFallbackChatClient);
