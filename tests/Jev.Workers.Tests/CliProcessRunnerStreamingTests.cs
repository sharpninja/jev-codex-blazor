using System.Diagnostics;
using Jev.Workers.Process;

namespace Jev.Workers.Tests;

public sealed class CliProcessRunnerStreamingTests
{
    [Fact]
    public async Task Fake_process_writes_are_reported_line_by_line()
    {
        var stdout = new List<string>();
        var stderr = new List<string>();
        var runner = new CliProcessRunner();
        var start = new ProcessStartInfo
        {
            FileName = "bash",
            ArgumentList = { "-lc", "printf 'out-a\\nout-b\\n'; printf 'err-a\\nerr-b\\n' >&2" }
        };

        var result = await runner.RunAsync(
            start,
            standardInput: null,
            new Progress<string>(stdout.Add),
            new Progress<string>(stderr.Add),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["out-a", "out-b"], stdout);
        Assert.Equal(["err-a", "err-b"], stderr);
        Assert.Contains("out-a", result.Stdout);
        Assert.Contains("err-a", result.Stderr);
    }
}
