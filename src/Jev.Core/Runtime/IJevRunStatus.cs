using Jev.Codex;

namespace Jev.Core.Runtime;

public interface IJevRunStatus
{
    JevPhase Phase { get; }

    string? Detail { get; }

    IReadOnlyList<CodexProgress> CodexEvents { get; }

    string Orchestration { get; }

    bool CodexInstalled { get; }

    string? CodexVersion { get; }

    void SetPhase(JevPhase phase, string? detail = null);

    void ReportCodex(CodexProgress progress);

    void SetOrchestration(string orchestration);

    void SetCodexAvailability(bool installed, string? version);
}
