namespace Jev.Workers;

/// <summary>
/// Subscription/login commands for coding-worker CLIs. Workers authenticate with
/// vendor login flows, not API keys.
/// </summary>
public static class SubscriptionAuth
{
    public static string LoginCommand(CodingStrategyKind kind) => kind switch
    {
        CodingStrategyKind.Codex => "codex login",
        CodingStrategyKind.Claude => "claude auth login",
        CodingStrategyKind.GrokBuild => "grok login",
        CodingStrategyKind.Cline => "cline auth",
        _ => "login"
    };

    public static string SubscriptionName(CodingStrategyKind kind) => kind switch
    {
        CodingStrategyKind.Codex => "ChatGPT",
        CodingStrategyKind.Claude => "Claude",
        CodingStrategyKind.GrokBuild => "SuperGrok / X Premium+",
        CodingStrategyKind.Cline => "Cline",
        _ => "subscription"
    };

    public static string NotLoggedInMessage(CodingStrategyKind kind, string displayName)
        => $"{displayName} is installed but not signed in. Run `{LoginCommand(kind)}` once with a {SubscriptionName(kind)} subscription. Do not set an API key for this worker.";

    public static string LoginHintWhenStatusUnknown(CodingStrategyKind kind, string displayName)
        => $"{displayName} is installed. This CLI does not expose a login-status command; run `{LoginCommand(kind)}` once with a {SubscriptionName(kind)} subscription if you have not already.";

    public static bool LooksLikeAuthFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not signed in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please log in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please login", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please sign in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("login required", StringComparison.OrdinalIgnoreCase)
               || text.Contains("authentication required", StringComparison.OrdinalIgnoreCase)
               || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
               || text.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase)
               || text.Contains("auth failed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase);
    }
}
