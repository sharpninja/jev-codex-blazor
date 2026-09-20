using System.Text.RegularExpressions;

namespace Jev.Workers.Process;

/// <summary>
/// Redacts tokens and credential-shaped values so CLI transcripts never echo secrets.
/// </summary>
public static partial class SecretSanitizer
{
    public const string Redacted = "[redacted]";

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var redacted = BearerToken().Replace(text, $"Bearer {Redacted}");
        redacted = Assignment().Replace(redacted, match => $"{match.Groups[1].Value}{match.Groups[2].Value}{Redacted}");
        redacted = KnownPrefixes().Replace(redacted, Redacted);
        return redacted;
    }

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex(
        @"\b(api[_-]?key|access[_-]?token|auth[_-]?token|secret|password|passwd|authorization|openai_api_key|anthropic_api_key|xai_api_key|github_token|hf_token)\b(\s*[:=]\s*)([^\s,;""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Assignment();

    [GeneratedRegex(
        @"\b(sk-[A-Za-z0-9_-]{8,}|sk-ant-[A-Za-z0-9_-]{8,}|xai-[A-Za-z0-9_-]{8,}|ghp_[A-Za-z0-9]{20,}|gho_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[baprs]-[A-Za-z0-9-]{10,})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex KnownPrefixes();
}
