using Jev.Workers.Cli;

namespace Jev.Workers.Tests;

public sealed class ClaudeCommandBuilderTests
{
    [Fact]
    public void BuildPrintArguments_uses_headless_print_contract()
    {
        var builder = new ClaudeCommandBuilder(new ClaudeCliOptions
        {
            Bare = true,
            OutputFormat = "json",
            PermissionMode = "acceptEdits",
            AllowedTools = "Read,Edit,Bash"
        });

        var args = builder.BuildPrintArguments(new CodingTaskRequest
        {
            Prompt = "scaffold hello",
            SessionId = "sess-1",
            Model = "claude-opus-4"
        });

        Assert.Equal(
            [
                "--bare", "-p", "scaffold hello",
                "--output-format", "json",
                "--permission-mode", "acceptEdits",
                "--allowedTools", "Read,Edit,Bash",
                "--model", "claude-opus-4",
                "--resume", "sess-1"
            ],
            args);
    }
}
