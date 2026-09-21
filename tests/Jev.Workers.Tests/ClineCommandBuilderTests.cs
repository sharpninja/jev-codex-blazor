using Jev.Workers.Cli;

namespace Jev.Workers.Tests;

public sealed class ClineCommandBuilderTests
{
    [Fact]
    public void BuildTaskArguments_uses_headless_json_yolo_without_api_key_flags()
    {
        var builder = new ClineCommandBuilder(new ClineCliOptions
        {
            Json = true,
            Yolo = true,
            AutoApprove = true,
            TimeoutSeconds = 300
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
                "--model", "claude-sonnet",
                "--id", "c-1",
                "run tests"
            ],
            args);
        Assert.DoesNotContain("--provider", args);
        Assert.DoesNotContain("--key", args);
        Assert.DoesNotContain("-k", args);
        Assert.DoesNotContain("-P", args);
    }

    [Fact]
    public void BuildAuthStatusArguments_is_absent_because_cline_has_no_status_command()
        => Assert.Null(new ClineCommandBuilder(new ClineCliOptions()).BuildAuthStatusArguments());

    [Fact]
    public void BuildVersionArguments_uses_cline_version_then_dashed_fallbacks()
    {
        var builder = new ClineCommandBuilder(new ClineCliOptions());
        Assert.Equal(["version"], builder.BuildVersionArguments());
        Assert.Equal(
            [
                ["version"],
                ["--version"],
                ["-V"]
            ],
            builder.BuildVersionArgumentCandidates());
    }
}
