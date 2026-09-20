using Jev.Workers;

namespace Jev.Core.Runtime;

public sealed class JevRunStatus : IJevRunStatus
{
    private readonly List<CodingProgress> _events = [];
    private readonly object _gate = new();

    public JevPhase Phase { get; private set; } = JevPhase.Idle;

    public string? Detail { get; private set; }

    public string Orchestration { get; private set; } = "unknown";

    public string ActiveStrategy { get; private set; } = "Codex";

    public bool WorkerInstalled { get; private set; }

    public string? WorkerVersion { get; private set; }

    public IReadOnlyList<CodingProgress> WorkerEvents
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public void SetPhase(JevPhase phase, string? detail = null)
    {
        lock (_gate)
        {
            Phase = phase;
            if (detail is not null || phase is JevPhase.Idle or JevPhase.Thinking)
            {
                Detail = detail;
            }

            if (phase is JevPhase.Thinking)
            {
                _events.Clear();
            }
        }
    }

    public void ReportWorker(CodingProgress progress)
    {
        lock (_gate)
        {
            Phase = JevPhase.RunningWorker;
            Detail = progress.Message;
            _events.Add(progress);
            if (_events.Count > 40)
            {
                _events.RemoveAt(0);
            }
        }
    }

    public void SetOrchestration(string orchestration) => Orchestration = orchestration;

    public void SetActiveStrategy(string strategy) => ActiveStrategy = strategy;

    public void SetWorkerAvailability(bool installed, string? version)
    {
        WorkerInstalled = installed;
        WorkerVersion = version;
    }
}
