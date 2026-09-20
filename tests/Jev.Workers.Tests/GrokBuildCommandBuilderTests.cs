using Jev.Workers.Cli;

namespace Jev.Workers.Tests;

public sealed class GrokBuildCommandBuilderTests
{
    [Fact]
    public void BuildPrintArguments_uses_grok_headless_contract()
    {
        var builder = new GrokBuildCommandBuilder(new GrokBuildCliOptions
        {
            OutputFormat = "streaming-json",
            AlwaysApprove = true
        });

        var args = builder.BuildPrintArguments(
            new CodingTaskRequest { Prompt = "explain this repo", SessionId = "g-1", Model = "grok-4.6" },
            "/tmp/work");

        Assert.Equal(
            [
                "-p", "explain this repo",
                "--output-format", "streaming-json",
                "--cwd", "/tmp/work",
                "--always-approve",
                "-m", "grok-4.6",
                "--resume", "g-1"
            ],
            args);
    }

    [Fact]
    public void BuildAuthStatusArguments_is_absent_because_grok_has_no_status_command()
        => Assert.Null(new GrokBuildCommandBuilder(new GrokBuildCliOptions()).BuildAuthStatusArguments());
}
