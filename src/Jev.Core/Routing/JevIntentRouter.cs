namespace Jev.Core.Routing;

/// <summary>
/// Deterministic tool picker used by the local fallback chat client.
/// The OpenAI-backed ChatClientAgent ignores this and chooses tools itself.
/// </summary>
public static class JevIntentRouter
{
    public static JevToolChoice Choose(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return JevToolChoice.None;
        }

        var text = userMessage.Trim();
        var lower = text.ToLowerInvariant();

        if (IsAvailabilityQuestion(lower))
        {
            return JevToolChoice.ProbeCodex;
        }

        if (IsHelloConsoleDemo(lower))
        {
            return JevToolChoice.ScaffoldHelloConsole;
        }

        return LooksLikeCodingWork(lower) ? JevToolChoice.RunCodingTask : JevToolChoice.None;
    }

    private static bool IsAvailabilityQuestion(string lower)
        => (lower.Contains("codex") || lower.Contains("cli"))
           && (lower.Contains("install")
               || lower.Contains("available")
               || lower.Contains("status")
               || lower.Contains("ready")
               || lower.Contains("probe")
               || lower.Contains("version"));

    private static bool IsHelloConsoleDemo(string lower)
        => (lower.Contains("hello") && (lower.Contains("console") || lower.Contains("world")))
           || (lower.Contains("scaffold") && lower.Contains("console"))
           || lower.Contains("demo coding task")
           || lower.Contains("try the integration");

    private static bool LooksLikeCodingWork(string lower)
    {
        string[] verbs =
        [
            "scaffold", "implement", "create", "write", "fix", "refactor",
            "generate", "patch", "edit", "add", "build", "compile", "test",
            "debug", "migrate"
        ];

        string[] nouns =
        [
            "app", "project", "file", "code", "class", "function", "workspace",
            "repo", "program", "solution", "bug", "test", "diff"
        ];

        return verbs.Any(lower.Contains) && nouns.Any(lower.Contains);
    }
}
