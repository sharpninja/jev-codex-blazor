namespace Jev.Workers;

public interface ICodingAgentStrategy
{
    CodingStrategyKind Kind { get; }

    string DisplayName { get; }

    string ExecutablePath { get; }

    /// <summary>Vendor subscription login command, e.g. <c>codex login</c>.</summary>
    string LoginCommand { get; }

    Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default);

    Task<CodingTaskResult> RunAsync(
        CodingTaskRequest request,
        IProgress<CodingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
