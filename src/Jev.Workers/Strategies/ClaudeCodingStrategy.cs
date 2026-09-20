using Jev.Workers.Cli;
using Jev.Workers.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Strategies;

public sealed class ClaudeCodingStrategy(
    IOptions<ClaudeCliOptions> optionsAccessor,
    ICliProcessRunner processRunner,
    ILogger<ClaudeCodingStrategy> logger,
    ICodingHost? host = null) : ProcessCodingStrategy(processRunner, logger, host)
{
    private readonly ClaudeCliOptions _options = optionsAccessor.Value;
    private readonly ClaudeCommandBuilder _commands = new(optionsAccessor.Value);

    public override CodingStrategyKind Kind => CodingStrategyKind.Claude;

    public override string DisplayName => "Claude Code";

    public override string ExecutablePath => _options.ExecutablePath;

    protected override int TimeoutSeconds => _options.TimeoutSeconds;

    protected override string? DefaultWorkspaceRoot => _options.DefaultWorkspaceRoot;

    protected override IReadOnlyList<string> BuildVersionArguments() => _commands.BuildVersionArguments();

    protected override IReadOnlyList<string>? BuildAuthStatusArguments() => _commands.BuildAuthStatusArguments();

    protected override IReadOnlyList<string> BuildExecArguments(CodingTaskRequest request, string workingDirectory)
        => _commands.BuildPrintArguments(request);
}
