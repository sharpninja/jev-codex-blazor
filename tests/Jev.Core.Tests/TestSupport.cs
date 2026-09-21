using Jev.Workers;

namespace Jev.Core.Tests;

internal sealed class RecordingStrategy : ICodingAgentStrategy
{
    public List<string> Prompts { get; } = [];

    public List<CodingTaskRequest> Requests { get; } = [];

    public bool Probed { get; private set; }

    public bool Unsupported { get; init; }

    public bool Missing { get; init; }

    public bool NotLoggedIn { get; init; }

    public IReadOnlyList<CodingProgress> ProgressToReport { get; init; } = [];

    public bool RunFailed { get; init; }

    public string? RunError { get; init; }

    public string? RunFinalMessage { get; init; }

    public string DefaultWorkspace { get; init; } = "/tmp/jev-test";

    public CodingStrategyKind Kind { get; init; } = CodingStrategyKind.Codex;

    public string DisplayName { get; init; } = "Codex";

    public string ExecutablePath => "codex";

    public string LoginCommand => "codex login";

    public Task<CodingAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        Probed = true;
        if (Unsupported)
        {
            return Task.FromResult(new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = false,
                IsSupported = false,
                IsLoggedIn = false,
                ExecutablePath = ExecutablePath,
                LoginCommand = LoginCommand,
                Error = CodingHost.UnsupportedMessage(CodingHost.Restricted("wasm"))
            });
        }

        if (Missing)
        {
            return Task.FromResult(new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = false,
                IsLoggedIn = false,
                ExecutablePath = ExecutablePath,
                LoginCommand = LoginCommand,
                Error = $"{DisplayName} CLI was not found on PATH."
            });
        }

        if (NotLoggedIn)
        {
            return Task.FromResult(new CodingAvailability
            {
                Kind = Kind,
                IsInstalled = true,
                IsLoggedIn = false,
                Version = "test 0.0",
                ExecutablePath = ExecutablePath,
                LoginCommand = LoginCommand,
                Error = $"{DisplayName} CLI is installed but not logged in. Run `{LoginCommand}`."
            });
        }

        return Task.FromResult(new CodingAvailability
        {
            Kind = Kind,
            IsInstalled = true,
            IsLoggedIn = true,
            Version = "test 0.0",
            ExecutablePath = ExecutablePath,
            LoginCommand = LoginCommand
        });
    }

    public Task<CodingTaskResult> RunAsync(
        CodingTaskRequest request,
        IProgress<CodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        Prompts.Add(request.Prompt);
        if (ProgressToReport.Count == 0)
        {
            progress?.Report(new CodingProgress("item.started", "fake worker running"));
        }
        else
        {
            foreach (var item in ProgressToReport)
            {
                progress?.Report(item);
            }
        }

        return Task.FromResult(new CodingTaskResult
        {
            Succeeded = !RunFailed,
            ExitCode = RunFailed ? 1 : 0,
            SessionId = "thread-test",
            FinalMessage = RunFinalMessage ?? "I'm Jev, simulated by Codex. Created Program.cs",
            Error = RunFailed ? RunError ?? $"{DisplayName} exited 1." : null,
            CommandLine = "codex exec --json -",
            WorkingDirectory = request.WorkingDirectory ?? DefaultWorkspace,
            Strategy = DisplayName,
            ChangedFiles = ["Program.cs"],
            CommandsRun = ["dotnet new console"],
            Stderr = RunFailed ? "agent failed" : ""
        });
    }
}

internal sealed class FixedSelector(ICodingAgentStrategy strategy) : ICodingStrategySelector
{
    public CodingStrategyKind ActiveKind => strategy.Kind;

    public ICodingAgentStrategy Active => strategy;

    public IReadOnlyList<ICodingAgentStrategy> All => [strategy];

    public IReadOnlyDictionary<CodingStrategyKind, CodingAvailability> Availability { get; } =
        new Dictionary<CodingStrategyKind, CodingAvailability>();

    public void Select(CodingStrategyKind kind)
    {
    }

    public Task RefreshAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
