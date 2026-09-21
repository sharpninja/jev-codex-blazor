using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Jev.Workers;

namespace Jev.App.Chat;

public sealed class JevChatService(
    JevCliSimulator simulator,
    IJevRunStatus status,
    ICodingStrategySelector selector,
    ICodingHost host,
    ICliTranscript transcript)
{
    public IJevRunStatus Status => status;

    public string Simulator => simulator.Label;

    public ICodingStrategySelector Selector => selector;

    public ICodingHost Host => host;

    public ICliTranscript Transcript => transcript;

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
        status.SetPhase(JevPhase.Thinking, $"Sending to {selector.Active.DisplayName}");
        await onChanged();

        try
        {
            var result = await simulator.SendAsync(trimmed, cancellationToken);
            assistant.Text = string.IsNullOrWhiteSpace(result.Text)
                ? $"{selector.Active.DisplayName} returned an empty reply."
                : result.Text;
            assistant.IsError = !result.Succeeded;
            assistant.IsStreaming = false;
            if (status.Phase != JevPhase.Error && result.Succeeded)
            {
                status.SetPhase(JevPhase.Done, "Done");
            }
        }
        catch (Exception ex)
        {
            assistant.IsError = true;
            assistant.IsStreaming = false;
            assistant.Text = string.IsNullOrWhiteSpace(assistant.Text)
                ? $"{selector.Active.DisplayName} hit an error: {ex.Message}"
                : assistant.Text + $"{Environment.NewLine}{Environment.NewLine}Error: {ex.Message}";
            status.SetPhase(JevPhase.Error, ex.Message);
        }

        await onChanged();
    }

    public Task ResetAsync()
    {
        Turns.Clear();
        simulator.Reset();
        return Task.CompletedTask;
    }

    private void ApplyActiveAvailability()
    {
        if (selector.Availability.TryGetValue(selector.ActiveKind, out var availability))
        {
            status.SetWorkerAvailability(
                availability.IsInstalled,
                availability.IsLoggedIn == false ? availability.Error : availability.Version ?? availability.Error,
                availability.IsLoggedIn,
                availability.LoginCommand);
        }

        status.SetActiveStrategy(selector.Active.DisplayName);
        status.SetSimulator(simulator.Label);
    }
}
