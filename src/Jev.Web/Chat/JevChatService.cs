using Jev.Core.Agent;
using Jev.Core.Runtime;
using Jev.Workers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Jev.Web.Chat;

public sealed class JevChatService(
    JevAgentHandle handle,
    IJevRunStatus status,
    ICodingStrategySelector selector)
{
    private AgentSession? _session;

    public IJevRunStatus Status => status;

    public string Orchestration => handle.Orchestration;

    public bool UsesFallback => handle.UsesFallbackChatClient;

    public ICodingStrategySelector Selector => selector;

    public IList<ChatTurn> Turns { get; } = [];

    public async Task EnsureProbedAsync(CancellationToken cancellationToken = default)
    {
        await selector.RefreshAvailabilityAsync(cancellationToken);
        ApplyActiveAvailability();
    }

    public async Task SwitchStrategyAsync(CodingStrategyKind kind, CancellationToken cancellationToken = default)
    {
        if (selector.ActiveKind == kind)
        {
            return;
        }

        selector.Select(kind);
        status.SetActiveStrategy(selector.Active.DisplayName);
        await ResetAsync();
        await selector.RefreshAvailabilityAsync(cancellationToken);
        ApplyActiveAvailability();
    }

    public async Task SendAsync(string userText, Func<Task> onChanged, CancellationToken cancellationToken)
    {
        var trimmed = userText.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        Turns.Add(new ChatTurn { Role = "user", Text = trimmed });
        var assistant = new ChatTurn { Role = "jev", IsStreaming = true };
        Turns.Add(assistant);
        status.SetPhase(JevPhase.Thinking, "Jev is planning");
        await onChanged();

        _session ??= await handle.Agent.CreateSessionAsync(cancellationToken);

        try
        {
            await foreach (var update in handle.Agent.RunStreamingAsync(trimmed, _session, cancellationToken: cancellationToken))
            {
                if (update.Contents.OfType<FunctionCallContent>().Any())
                {
                    status.SetPhase(JevPhase.RunningWorker, $"Calling {selector.Active.DisplayName}");
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    assistant.Text += update.Text;
                    await onChanged();
                }
            }

            if (string.IsNullOrWhiteSpace(assistant.Text))
            {
                assistant.Text = "Jev returned an empty reply.";
            }

            assistant.IsStreaming = false;
            if (status.Phase != JevPhase.Error)
            {
                status.SetPhase(JevPhase.Done, "Done");
            }
        }
        catch (Exception ex)
        {
            assistant.IsError = true;
            assistant.IsStreaming = false;
            assistant.Text = string.IsNullOrWhiteSpace(assistant.Text)
                ? $"Jev hit an error: {ex.Message}"
                : assistant.Text + $"{Environment.NewLine}{Environment.NewLine}Error: {ex.Message}";
            status.SetPhase(JevPhase.Error, ex.Message);
        }

        await onChanged();
    }

    public async Task ResetAsync()
    {
        Turns.Clear();
        if (_session is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_session is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _session = null;
        status.SetPhase(JevPhase.Idle, "New conversation");
    }

    private void ApplyActiveAvailability()
    {
        if (selector.Availability.TryGetValue(selector.ActiveKind, out var availability))
        {
            status.SetWorkerAvailability(availability.IsInstalled, availability.Version ?? availability.Error);
        }

        status.SetActiveStrategy(selector.Active.DisplayName);
    }
}
