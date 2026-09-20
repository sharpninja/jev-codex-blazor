using Jev.Codex;

namespace Jev.Core.Runtime;

public sealed class JevRunStatus : IJevRunStatus
{
    private readonly List<CodexProgress> _events = [];
    private readonly object _gate = new();

    public JevPhase Phase { get; private set; } = JevPhase.Idle;

    public string? Detail { get; private set; }

    public string Orchestration { get; private set; } = "unknown";

    public bool CodexInstalled { get; private set; }

    public string? CodexVersion { get; private set; }

    public IReadOnlyList<CodexProgress> CodexEvents
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

    public void ReportCodex(CodexProgress progress)
    {
        lock (_gate)
        {
            Phase = JevPhase.RunningCodex;
            Detail = progress.Message;
            _events.Add(progress);
            if (_events.Count > 40)
            {
                _events.RemoveAt(0);
            }
        }
    }

    public void SetOrchestration(string orchestration) => Orchestration = orchestration;

    public void SetCodexAvailability(bool installed, string? version)
    {
        CodexInstalled = installed;
        CodexVersion = version;
    }
}
