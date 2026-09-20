namespace Jev.Workers.Tests;

public sealed class CodingStrategySelectorTests
{
    [Fact]
    public void Selects_configured_strategy_and_can_switch()
    {
        var claude = new FakeStrategy(CodingStrategyKind.Claude, "Claude Code");
        var codex = new FakeStrategy(CodingStrategyKind.Codex, "Codex");
        var selector = new CodingStrategySelector([claude, codex], CodingStrategyKind.Claude);

        Assert.Equal(CodingStrategyKind.Claude, selector.ActiveKind);
        Assert.Same(claude, selector.Active);

        selector.Select(CodingStrategyKind.Codex);
        Assert.Equal(CodingStrategyKind.Codex, selector.ActiveKind);
        Assert.Same(codex, selector.Active);
    }

    [Fact]
    public async Task RefreshAvailability_probes_every_strategy()
    {
        var claude = new FakeStrategy(CodingStrategyKind.Claude, "Claude Code") { Installed = false };
        var codex = new FakeStrategy(CodingStrategyKind.Codex, "Codex") { Installed = true };
        var selector = new CodingStrategySelector([claude, codex], CodingStrategyKind.Codex);

        await selector.RefreshAvailabilityAsync();

        Assert.True(selector.Availability[CodingStrategyKind.Codex].IsInstalled);
        Assert.False(selector.Availability[CodingStrategyKind.Claude].IsInstalled);
        Assert.True(codex.Probed);
        Assert.True(claude.Probed);
    }

    private sealed class FakeStrategy : ICodingAgentStrategy
    {
        public FakeStrategy(CodingStrategyKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName;
        }

        public CodingStrategyKind Kind { get; }

        public string DisplayName { get; }

        public string ExecutablePath => Kind.ToString().ToLowerInvariant();

        public string LoginCommand => SubscriptionAuth.LoginCommand(Kind);

        public bool Installed { get; set; }

        public bool Probed { get; private set; }

        public Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default)
        {
            Probed = true;
            return Task.FromResult(new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = Installed,
                ExecutablePath = ExecutablePath
            });
        }

        public Task<CodingTaskResult> RunAsync(
            CodingTaskRequest request,
            IProgress<CodingProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CodingTaskResult { Succeeded = true, Strategy = DisplayName });
    }
}
