namespace Jev.Workers.Tests;

public sealed class SubscriptionAuthTests
{
    [Theory]
    [InlineData(CodingStrategyKind.Codex, "codex login")]
    [InlineData(CodingStrategyKind.Claude, "claude auth login")]
    [InlineData(CodingStrategyKind.GrokBuild, "grok login")]
    [InlineData(CodingStrategyKind.Cline, "cline auth")]
    public void LoginCommand_is_subscription_only(CodingStrategyKind kind, string expected)
    {
        Assert.Equal(expected, SubscriptionAuth.LoginCommand(kind));
        var message = SubscriptionAuth.NotLoggedInMessage(kind, kind.ToString());
        Assert.Contains(expected, message, StringComparison.Ordinal);
        Assert.Contains("subscription", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPENAI_API_KEY", message, StringComparison.Ordinal);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", message, StringComparison.Ordinal);
        Assert.DoesNotContain("XAI_API_KEY", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("please sign in to continue")]
    [InlineData("Unauthorized")]
    [InlineData("not logged in")]
    public void LooksLikeAuthFailure_detects_login_errors(string text)
        => Assert.True(SubscriptionAuth.LooksLikeAuthFailure(text));

    [Fact]
    public void LooksLikeAuthFailure_ignores_generic_errors()
        => Assert.False(SubscriptionAuth.LooksLikeAuthFailure("file not found: Program.cs"));
}
