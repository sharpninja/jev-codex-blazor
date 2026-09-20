using Jev.Workers;

namespace Jev.Core.Tests;

internal sealed class RecordingStrategy : ICodingAgentStrategy
{
    public List<string> Prompts { get; } = [];

    public bool Probed { get; private set; }

    public bool Unsupported { get; init; }

    public IReadOnlyList<CodingProgress> ProgressToReport { get; init; } = [];

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

        return Task.FromResult(new CodingAvailability
        {
            Kind = Kind,
            IsInstalled = true,
            Version = "test 0.0",
            ExecutablePath = ExecutablePath
        });
    }

    public Task<CodingTaskResult> RunAsync(
        CodingTaskRequest request,
        IProgress<CodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
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
            Succeeded = true,
            ExitCode = 0,
            SessionId = "thread-test",
            FinalMessage = "Created Program.cs",
            CommandLine = "codex exec --json -",
            WorkingDirectory = "/tmp/jev-test",
            Strategy = DisplayName,
            ChangedFiles = ["Program.cs"],
            CommandsRun = ["dotnet new console"]
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
