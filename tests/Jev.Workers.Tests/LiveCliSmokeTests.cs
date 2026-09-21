using Jev.Codex;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Tests;

/// <summary>
/// Optional smoke against a real CLI on PATH. Always skipped in CI when the
/// binary is missing — stubbed <see cref="StrategyProcessCallTests"/> are the gate.
/// </summary>
public sealed class LiveCliSmokeTests
{
    [Fact]
    [Trait("Live", "Cli")]
    public Task Codex_live_probe_skips_when_not_installed()
        => ProbeLiveAsync(CodingStrategyKind.Codex);

    [Fact]
    [Trait("Live", "Cli")]
    public Task Claude_live_probe_skips_when_not_installed()
        => ProbeLiveAsync(CodingStrategyKind.Claude);

    [Fact]
    [Trait("Live", "Cli")]
    public Task GrokBuild_live_probe_skips_when_not_installed()
        => ProbeLiveAsync(CodingStrategyKind.GrokBuild);

    [Fact]
    [Trait("Live", "Cli")]
    public Task Cline_live_probe_skips_when_not_installed()
        => ProbeLiveAsync(CodingStrategyKind.Cline);

    private static async Task ProbeLiveAsync(CodingStrategyKind kind)
    {
        var strategy = CreateDefaultStrategy(kind);
        var availability = await strategy.ProbeAsync();
        if (!availability.IsInstalled)
        {
            return;
        }

        Assert.True(availability.IsSupported);
        Assert.Equal(kind, availability.Kind);
        Assert.False(string.IsNullOrWhiteSpace(availability.ExecutablePath));
    }

    private static ICodingAgentStrategy CreateDefaultStrategy(CodingStrategyKind kind)
        => kind switch
        {
            CodingStrategyKind.Codex => CreateCodex(),
            CodingStrategyKind.Claude => new ClaudeCodingStrategy(
                Options.Create(new ClaudeCliOptions()),
                new CliProcessRunner(),
                NullLogger<ClaudeCodingStrategy>.Instance,
                CodingHost.Desktop("live")),
            CodingStrategyKind.GrokBuild => new GrokBuildCodingStrategy(
                Options.Create(new GrokBuildCliOptions()),
                new CliProcessRunner(),
                NullLogger<GrokBuildCodingStrategy>.Instance,
                CodingHost.Desktop("live")),
            CodingStrategyKind.Cline => new ClineCodingStrategy(
                Options.Create(new ClineCliOptions()),
                new CliProcessRunner(),
                NullLogger<ClineCodingStrategy>.Instance,
                CodingHost.Desktop("live")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static CodexCodingStrategy CreateCodex()
    {
        var options = Options.Create(new CodexCliOptions { TimeoutSeconds = 15 });
        return new CodexCodingStrategy(
            new CodexCliClient(options, new CodexProcessRunner(), NullLogger<CodexCliClient>.Instance),
            options,
            CodingHost.Desktop("live"));
    }
}
