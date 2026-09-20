using Jev.Codex;

namespace Jev.Codex.Tests;

public sealed class CodexCommandBuilderTests
{
    [Fact]
    public void BuildExecArguments_uses_stdin_prompt_and_safe_defaults()
    {
        var builder = new CodexCommandBuilder(new CodexCliOptions
        {
            DefaultSandbox = "workspace-write",
            AskForApproval = "never",
            SkipGitRepoCheck = true,
            UseJsonEvents = true
        });

        var args = builder.BuildExecArguments(
            new CodexExecRequest { Prompt = "scaffold hello" },
            "/tmp/workspace");

        Assert.Equal(
            [
                "exec", "--json", "--color", "never",
                "--sandbox", "workspace-write",
                "--ask-for-approval", "never",
                "--skip-git-repo-check",
                "--cd", "/tmp/workspace",
                "-"
            ],
            args);
    }

    [Fact]
    public void BuildExecArguments_clamps_dangerous_sandbox_unless_allowed()
    {
        var builder = new CodexCommandBuilder(new CodexCliOptions { AllowDangerousSandbox = false });

        var args = builder.BuildExecArguments(
            new CodexExecRequest { Prompt = "x", Sandbox = "danger-full-access", SessionId = "thread-1", Model = "gpt-5" },
            "/work");

        Assert.Contains("--sandbox", args);
        Assert.Equal("workspace-write", args[args.ToList().IndexOf("--sandbox") + 1]);
        Assert.Contains("resume", args);
        Assert.Contains("thread-1", args);
        Assert.Contains("--model", args);
        Assert.Equal("-", args[^1]);
    }
}
