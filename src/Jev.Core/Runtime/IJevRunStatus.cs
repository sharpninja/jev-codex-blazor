using Jev.Workers;

namespace Jev.Core.Runtime;

public interface IJevRunStatus
{
    JevPhase Phase { get; }

    string? Detail { get; }

    IReadOnlyList<CodingProgress> WorkerEvents { get; }

    string Simulator { get; }

    string ActiveStrategy { get; }

    bool WorkerInstalled { get; }

    bool? WorkerLoggedIn { get; }

    string? WorkerLoginCommand { get; }

    string? WorkerVersion { get; }

    void SetPhase(JevPhase phase, string? detail = null);

    void ReportWorker(CodingProgress progress);

    void SetSimulator(string simulator);

    void SetActiveStrategy(string strategy);

    void SetWorkerAvailability(bool installed, string? version, bool? loggedIn = null, string? loginCommand = null);
}
