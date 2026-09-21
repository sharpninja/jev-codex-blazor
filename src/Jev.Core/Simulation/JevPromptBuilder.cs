using System.Text;

namespace Jev.Core.Simulation;

/// <summary>
/// Builds the prompt sent to the selected coding-worker CLI. Jev is a persona
/// preamble, not a second chat model.
/// </summary>
public static class JevPromptBuilder
{
    public const int MaxHistoryTurns = 16;

    public static string Build(
        string personaInstructions,
        string workerDisplayName,
        IReadOnlyList<JevHistoryTurn> history,
        string userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var builder = new StringBuilder();
        builder.AppendLine("You are running as Jev. This CLI process is the conversational and coding brain for this turn.");
        builder.AppendLine("There is no separate orchestration model and no fallback router. Answer the user directly as Jev.");
        builder.AppendLine();
        builder.AppendLine("## Jev instructions");
        builder.AppendLine(personaInstructions.Trim());
        builder.AppendLine();
        builder.AppendLine("## Active worker");
        builder.AppendLine($"You are the {workerDisplayName} CLI simulating Jev. Name this worker when you report file or command results.");
        builder.AppendLine();

        var recent = history.Count <= MaxHistoryTurns
            ? history
            : history.Skip(history.Count - MaxHistoryTurns).ToList();
        if (recent.Count > 0)
        {
            builder.AppendLine("## Conversation so far");
            foreach (var turn in recent)
            {
                builder.Append(turn.Role);
                builder.Append(": ");
                builder.AppendLine(turn.Text);
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Current user message");
        builder.AppendLine(userMessage.Trim());
        return builder.ToString();
    }
}
