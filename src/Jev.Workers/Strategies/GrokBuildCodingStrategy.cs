using Jev.Workers.Cli;
using Jev.Workers.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Strategies;

public sealed class GrokBuildCodingStrategy(
    IOptions<GrokBuildCliOptions> optionsAccessor,
    ICliProcessRunner processRunner,
    ILogger<GrokBuildCodingStrategy> logger) : ProcessCodingStrategy(processRunner, logger)
{
    private readonly GrokBuildCliOptions _options = optionsAccessor.Value;
    private readonly GrokBuildCommandBuilder _commands = new(optionsAccessor.Value);

    public override CodingStrategyKind Kind => CodingStrategyKind.GrokBuild;

    public override string DisplayName => "Grok Build";

    public override string ExecutablePath => _options.ExecutablePath;

    protected override int TimeoutSeconds => _options.TimeoutSeconds;

    protected override string? DefaultWorkspaceRoot => _options.DefaultWorkspaceRoot;

    protected override IReadOnlyList<string> BuildVersionArguments() => _commands.BuildVersionArguments();

    protected override IReadOnlyList<string> BuildExecArguments(CodingTaskRequest request, string workingDirectory)
        => _commands.BuildPrintArguments(request, workingDirectory);
}
