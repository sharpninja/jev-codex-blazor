using Jev.Workers.Cli;

namespace Jev.Workers.Tests;

public sealed class ClineCommandBuilderTests
{
    [Fact]
    public void BuildTaskArguments_uses_headless_json_yolo_contract()
    {
        var builder = new ClineCommandBuilder(new ClineCliOptions
        {
            Json = true,
            Yolo = true,
            AutoApprove = true,
            TimeoutSeconds = 300,
            Provider = "anthropic"
        });

        var args = builder.BuildTaskArguments(
            new CodingTaskRequest { Prompt = "run tests", SessionId = "c-1", Model = "claude-sonnet" },
            "/tmp/work");

        Assert.Equal(
            [
                "--json", "--yolo",
                "--auto-approve", "true",
                "--cwd", "/tmp/work",
                "--timeout", "300",
                "--provider", "anthropic",
                "--model", "claude-sonnet",
                "--id", "c-1",
                "run tests"
            ],
            args);
    }
}
