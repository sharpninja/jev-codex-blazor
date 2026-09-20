namespace Jev.Workers;

public sealed class CodingStrategySelector : ICodingStrategySelector
{
    private readonly Dictionary<CodingStrategyKind, ICodingAgentStrategy> _strategies;
    private readonly Dictionary<CodingStrategyKind, CodingAvailability> _availability = [];

    public CodingStrategySelector(IEnumerable<ICodingAgentStrategy> strategies, CodingStrategyKind initial)
    {
        _strategies = strategies.ToDictionary(strategy => strategy.Kind);
        if (_strategies.Count == 0)
        {
            throw new InvalidOperationException("No coding-agent strategies were registered.");
        }

        ActiveKind = _strategies.ContainsKey(initial) ? initial : _strategies.Keys.First();
    }

    public CodingStrategyKind ActiveKind { get; private set; }

    public ICodingAgentStrategy Active => _strategies[ActiveKind];

    public IReadOnlyList<ICodingAgentStrategy> All => _strategies.Values.OrderBy(strategy => strategy.Kind).ToArray();

    public IReadOnlyDictionary<CodingStrategyKind, CodingAvailability> Availability => _availability;

    public void Select(CodingStrategyKind kind)
    {
        if (!_strategies.ContainsKey(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown coding strategy.");
        }

        ActiveKind = kind;
    }

    public async Task RefreshAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        foreach (var strategy in All)
        {
            _availability[strategy.Kind] = await strategy.ProbeAsync(cancellationToken);
        }
    }
}
