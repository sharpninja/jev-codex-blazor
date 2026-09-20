using Jev.Workers.Cli;

namespace Jev.Workers.Tests;

public sealed class ClaudeCommandBuilderTests
{
    [Fact]
    public void BuildPrintArguments_uses_subscription_headless_print_contract()
    {
        var builder = new ClaudeCommandBuilder(new ClaudeCliOptions
        {
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
                "-p", "scaffold hello",
                "--output-format", "json",
                "--permission-mode", "acceptEdits",
                "--allowedTools", "Read,Edit,Bash",
                "--model", "claude-opus-4",
                "--resume", "sess-1"
            ],
            args);
        Assert.DoesNotContain("--bare", args);
    }

    [Fact]
    public void BuildAuthStatusArguments_uses_claude_auth_status()
        => Assert.Equal(["auth", "status"], new ClaudeCommandBuilder(new ClaudeCliOptions()).BuildAuthStatusArguments());
}
