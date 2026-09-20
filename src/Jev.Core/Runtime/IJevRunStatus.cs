using Jev.Workers;

namespace Jev.Core.Runtime;

public interface IJevRunStatus
{
    JevPhase Phase { get; }

    string? Detail { get; }

    IReadOnlyList<CodingProgress> WorkerEvents { get; }

    string Orchestration { get; }

    string ActiveStrategy { get; }

    bool WorkerInstalled { get; }

    string? WorkerVersion { get; }

    void SetPhase(JevPhase phase, string? detail = null);

    void ReportWorker(CodingProgress progress);

    void SetOrchestration(string orchestration);

    void SetActiveStrategy(string strategy);

    void SetWorkerAvailability(bool installed, string? version);
}
