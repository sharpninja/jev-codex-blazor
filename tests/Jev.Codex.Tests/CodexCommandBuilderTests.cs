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
                "--ask-for-approval", "never",
                "exec", "--json", "--color", "never",
                "--sandbox", "workspace-write",
                "--skip-git-repo-check",
                "--cd", "/tmp/workspace",
                "-"
            ],
            args);
    }

    [Fact]
    public void BuildExecArguments_omits_approval_flag_when_unset()
    {
        var builder = new CodexCommandBuilder(new CodexCliOptions
        {
            AskForApproval = " ",
            SkipGitRepoCheck = false,
            UseJsonEvents = false
        });

        var args = builder.BuildExecArguments(
            new CodexExecRequest { Prompt = "hello" },
            "/work");

        Assert.Equal("exec", args[0]);
        Assert.DoesNotContain("--ask-for-approval", args);
    }

    [Fact]
    public void BuildLoginStatusArguments_uses_codex_login_status()
        => Assert.Equal(["login", "status"], new CodexCommandBuilder(new CodexCliOptions()).BuildLoginStatusArguments());

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
