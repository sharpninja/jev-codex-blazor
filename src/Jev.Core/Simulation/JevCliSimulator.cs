using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Workers;
using Jev.Workers.Process;

namespace Jev.Core.Simulation;

/// <summary>
/// Drives the selected coding-worker CLI as Jev's conversational brain.
/// Persona text is a prompt preamble; there is no orchestration LLM.
/// </summary>
public sealed class JevCliSimulator(
    JevPersona persona,
    ICodingStrategySelector selector,
    IJevRunStatus status,
    ICliTranscript transcript)
{
    private readonly List<JevHistoryTurn> _history = [];
    private string? _sessionId;
    private string? _workspace;

    public string Label => $"{selector.Active.DisplayName} CLI";

    public IReadOnlyList<JevHistoryTurn> History => _history;

    public string? SessionId => _sessionId;

    public string? Workspace => _workspace;

    public void BindStatus()
    {
        status.SetSimulator(Label);
        status.SetActiveStrategy(selector.Active.DisplayName);
    }

    public async Task<JevSimulationTurn> SendAsync(string userText, CancellationToken cancellationToken = default)
    {
        var trimmed = userText.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(trimmed);

        BindStatus();
        status.SetPhase(JevPhase.Thinking, $"Preparing {selector.Active.DisplayName}");

        var availability = await selector.Active.ProbeAsync(cancellationToken);
        status.SetWorkerAvailability(
            availability.IsInstalled,
            availability.IsLoggedIn == false ? availability.Error : availability.Version ?? availability.Error,
            availability.IsLoggedIn,
            availability.LoginCommand);

        if (!availability.IsReady)
        {
            var error = availability.FormatForAgent();
            transcript.Append(CliTranscriptChannel.System, error);
            status.SetPhase(JevPhase.Error, error);
            Remember(trimmed, error);
            return JevSimulationTurn.Error(error);
        }

        var prompt = JevPromptBuilder.Build(
            persona.LoadInstructions(),
            selector.Active.DisplayName,
            _history,
            trimmed);

        status.SetPhase(JevPhase.RunningWorker, $"Calling {selector.Active.DisplayName}");
        var result = await selector.Active.RunAsync(
            new CodingTaskRequest
            {
                Prompt = prompt,
                WorkingDirectory = _workspace,
                SessionId = _sessionId
            },
            new ImmediateProgress<CodingProgress>(item =>
            {
                if (item.Phase is not (CodingProgress.Input or CodingProgress.Stdout or CodingProgress.Stderr))
                {
                    status.ReportWorker(item);
                }

                transcript.Append(item);
            }),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.SessionId))
        {
            _sessionId = result.SessionId;
        }

        if (!string.IsNullOrWhiteSpace(result.WorkingDirectory))
        {
            _workspace = result.WorkingDirectory;
        }

        var reply = ChooseReply(result);
        Remember(trimmed, reply);

        if (!result.Succeeded)
        {
            status.SetPhase(JevPhase.Error, result.Error ?? reply);
            return JevSimulationTurn.Error(reply);
        }

        status.SetPhase(JevPhase.Done, "Done");
        return JevSimulationTurn.Ok(reply);
    }

    public void Reset()
    {
        _history.Clear();
        _sessionId = null;
        _workspace = null;
        transcript.Clear();
        BindStatus();
        status.SetPhase(JevPhase.Idle, "New conversation");
    }

    private void Remember(string userText, string reply)
    {
        _history.Add(new JevHistoryTurn("user", userText));
        _history.Add(new JevHistoryTurn("jev", reply));
    }

    private static string ChooseReply(CodingTaskResult result)
    {
        if (!result.Succeeded)
        {
            return result.FormatForAgent();
        }

        return string.IsNullOrWhiteSpace(result.FinalMessage)
            ? result.FormatForAgent()
            : result.FinalMessage;
    }
}
