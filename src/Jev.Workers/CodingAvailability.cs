namespace Jev.Workers;

public sealed class CodingAvailability
{
    public required CodingStrategyKind Kind { get; init; }

    public required bool IsInstalled { get; init; }

    public bool IsSupported { get; init; } = true;

    /// <summary>
    /// <c>true</c> when the CLI reports a subscription session, <c>false</c> when
    /// it reports logged-out, <c>null</c> when the CLI has no status command.
    /// </summary>
    public bool? IsLoggedIn { get; init; }

    public string? Version { get; init; }

    public string ExecutablePath { get; init; } = "";

    public string LoginCommand { get; init; } = "";

    public string? Error { get; init; }

    public bool IsReady => IsInstalled && IsLoggedIn != false && string.IsNullOrWhiteSpace(Error);

    public string FormatForAgent()
    {
        if (!IsSupported)
        {
            return $"{Kind} worker is not supported on this host. {Error}";
        }

        if (!IsInstalled)
        {
            return $"{Kind} worker is not installed ({ExecutablePath}). {Error}";
        }

        if (IsLoggedIn == false)
        {
            return $"{Kind} worker is installed but not logged in. {Error ?? SubscriptionAuth.NotLoggedInMessage(Kind, Kind.ToString())}";
        }

        if (!string.IsNullOrWhiteSpace(Error))
        {
            return $"{Kind} worker is installed at '{ExecutablePath}' but a probe command failed. {Error}";
        }

        if (IsLoggedIn is null)
        {
            return $"{Kind} worker is available at '{ExecutablePath}'{(string.IsNullOrWhiteSpace(Version) ? "." : $": {Version}")} {SubscriptionAuth.LoginHintWhenStatusUnknown(Kind, Kind.ToString())}";
        }

        return $"{Kind} worker is available at '{ExecutablePath}'{(string.IsNullOrWhiteSpace(Version) ? "." : $": {Version}")} Subscription session is signed in (`{LoginCommand}`).";
    }
}
