using Jev.Codex;
using Jev.Core.Agent;
using Jev.Core.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Jev.Web.Chat;

public sealed class JevChatService(
    JevAgentHandle handle,
    IJevRunStatus status,
    ICodexCli codex)
{
    private AgentSession? _session;

    public IJevRunStatus Status => status;

    public string Orchestration => handle.Orchestration;

    public bool UsesFallback => handle.UsesFallbackChatClient;

    public IList<ChatTurn> Turns { get; } = [];

    public async Task EnsureProbedAsync(CancellationToken cancellationToken = default)
    {
        if (status.CodexInstalled || !string.IsNullOrWhiteSpace(status.CodexVersion))
        {
            return;
        }

        try
        {
            var availability = await codex.ProbeAsync(cancellationToken);
            status.SetCodexAvailability(availability.IsInstalled, availability.Version ?? availability.Error);
        }
        catch (Exception ex)
        {
            status.SetCodexAvailability(false, ex.Message);
        }
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
                    status.SetPhase(JevPhase.RunningCodex, "Calling Codex");
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
}
