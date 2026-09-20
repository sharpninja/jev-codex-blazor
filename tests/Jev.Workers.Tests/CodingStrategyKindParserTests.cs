using Jev.Workers;

namespace Jev.Workers.Tests;

public sealed class CodingStrategyKindParserTests
{
    [Theory]
    [InlineData("Codex", CodingStrategyKind.Codex)]
    [InlineData("claude", CodingStrategyKind.Claude)]
    [InlineData("ClaudeCode", CodingStrategyKind.Claude)]
    [InlineData("grok", CodingStrategyKind.GrokBuild)]
    [InlineData("GrokBuild", CodingStrategyKind.GrokBuild)]
    [InlineData("xai", CodingStrategyKind.GrokBuild)]
    [InlineData("cline", CodingStrategyKind.Cline)]
    public void Parses_known_strategy_names(string value, CodingStrategyKind expected)
        => Assert.Equal(expected, CodingStrategyKindParser.ParseOrDefault(value));

    [Fact]
    public void Unknown_name_falls_back_to_codex()
        => Assert.Equal(CodingStrategyKind.Codex, CodingStrategyKindParser.ParseOrDefault("not-a-worker"));
}
