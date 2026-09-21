namespace Jev.Codex;

public sealed class CodexAvailability
{
    public const string LoginCommandText = "codex login";

    public required bool IsInstalled { get; init; }

    public bool? IsLoggedIn { get; init; }

    public string? Version { get; init; }

    public string ExecutablePath { get; init; } = "codex";

    public string LoginCommand { get; init; } = LoginCommandText;

    public string? Error { get; init; }

    public string FormatForAgent()
    {
        if (!IsInstalled)
        {
            return $"Codex CLI is not available ({ExecutablePath}). {Error}";
        }

        if (IsLoggedIn == false)
        {
            return $"Codex CLI is installed but not logged in. {Error ?? $"Run `{LoginCommand}` once with a ChatGPT subscription. Do not set OPENAI_API_KEY for Codex."}";
        }

        if (!string.IsNullOrWhiteSpace(Error))
        {
            return $"Codex CLI is installed at '{ExecutablePath}' but a probe command failed. {Error}";
        }

        return $"Codex CLI is available at '{ExecutablePath}'{(string.IsNullOrWhiteSpace(Version) ? "." : $": {Version}")} Sign in with `{LoginCommand}` (ChatGPT subscription) if you have not already.";
    }
}
