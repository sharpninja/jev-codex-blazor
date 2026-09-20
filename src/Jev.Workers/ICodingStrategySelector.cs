namespace Jev.Workers;

public interface ICodingStrategySelector
{
    CodingStrategyKind ActiveKind { get; }

    ICodingAgentStrategy Active { get; }

    IReadOnlyList<ICodingAgentStrategy> All { get; }

    IReadOnlyDictionary<CodingStrategyKind, CodingAvailability> Availability { get; }

    void Select(CodingStrategyKind kind);

    Task RefreshAvailabilityAsync(CancellationToken cancellationToken = default);
}
