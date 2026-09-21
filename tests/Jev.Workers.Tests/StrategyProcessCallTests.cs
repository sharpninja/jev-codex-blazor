using System.Text;
using Jev.Codex;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Workers.Tests;

public sealed class StrategyProcessCallTests
{
    public const string NonAsciiPrompt = "Who are you? — café";

    [Fact]
    public async Task Codex_probe_and_run_uses_approval_before_exec_and_utf8_stdin()
        => await ProbeAndRunAsync(CodingStrategyKind.Codex);

    [Fact]
    public async Task Claude_probe_and_run_uses_headless_print_contract()
        => await ProbeAndRunAsync(CodingStrategyKind.Claude);

    [Fact]
    public async Task GrokBuild_probe_and_run_uses_headless_print_contract()
        => await ProbeAndRunAsync(CodingStrategyKind.GrokBuild);

    [Fact]
    public async Task Cline_probe_and_run_uses_yolo_json_contract()
        => await ProbeAndRunAsync(CodingStrategyKind.Cline);

    private static async Task ProbeAndRunAsync(CodingStrategyKind kind)
    {
        using var stub = new CliCallStub(kind);
        using var workspace = new TempWorkspace();
        var strategy = CreateStrategy(kind, stub);
        var request = new CodingTaskRequest
        {
            Prompt = NonAsciiPrompt,
            WorkingDirectory = workspace.Path,
            Timeout = TimeSpan.FromSeconds(20)
        };

        var availability = await strategy.ProbeAsync();
        Assert.True(availability.IsInstalled, availability.FormatForAgent());
        Assert.True(availability.IsReady, availability.FormatForAgent());
        Assert.Equal(kind, availability.Kind);
        Assert.Contains("stub 0.0", availability.Version, StringComparison.OrdinalIgnoreCase);
        if (kind is CodingStrategyKind.Codex or CodingStrategyKind.Claude)
        {
            Assert.True(availability.IsLoggedIn);
        }
        else
        {
            Assert.Null(availability.IsLoggedIn);
        }

        var result = await strategy.RunAsync(request);

        Assert.True(result.Succeeded, result.Error ?? result.FormatForAgent());
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(stub.SuccessMarker, result.FinalMessage);
        Assert.Equal(stub.SessionId, result.SessionId);
        Assert.Equal(workspace.Path, result.WorkingDirectory, ignoreCase: OperatingSystem.IsWindows());

        var expected = ExpectedExecArguments(kind, request, result.WorkingDirectory);
        foreach (var argument in expected)
        {
            Assert.Contains(argument, result.CommandLine, StringComparison.Ordinal);
        }

        var captured = stub.TryReadArgv();
        if (captured is not null)
        {
            Assert.Equal(expected, captured);
        }

        if (kind == CodingStrategyKind.Codex)
        {
            var stdin = stub.ReadStdin();
            Assert.False(HasUtf8Bom(stdin));
            var decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(stdin);
            Assert.Equal(NonAsciiPrompt, decoded);
            Assert.True(stdin.AsSpan().IndexOf("—"u8) >= 0);
            Assert.True(stdin.AsSpan().IndexOf("é"u8) >= 0);
        }
    }

    private static ICodingAgentStrategy CreateStrategy(CodingStrategyKind kind, CliCallStub stub)
    {
        var env = stub.IsolatedEnvironment;
        return kind switch
        {
            CodingStrategyKind.Codex => CreateCodex(stub, env),
            CodingStrategyKind.Claude => new ClaudeCodingStrategy(
                Options.Create(new ClaudeCliOptions
                {
                    ExecutablePath = stub.ExecutablePath,
                    TimeoutSeconds = 20
                }),
                new CliProcessRunner(env),
                NullLogger<ClaudeCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            CodingStrategyKind.GrokBuild => new GrokBuildCodingStrategy(
                Options.Create(new GrokBuildCliOptions
                {
                    ExecutablePath = stub.ExecutablePath,
                    TimeoutSeconds = 20
                }),
                new CliProcessRunner(env),
                NullLogger<GrokBuildCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            CodingStrategyKind.Cline => new ClineCodingStrategy(
                Options.Create(new ClineCliOptions
                {
                    ExecutablePath = stub.ExecutablePath,
                    TimeoutSeconds = 20
                }),
                new CliProcessRunner(env),
                NullLogger<ClineCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static CodexCodingStrategy CreateCodex(CliCallStub stub, CliSearchEnvironment env)
    {
        var options = Options.Create(new CodexCliOptions
        {
            ExecutablePath = stub.ExecutablePath,
            TimeoutSeconds = 20
        });
        return new CodexCodingStrategy(
            new CodexCliClient(options, new CodexProcessRunner(env), NullLogger<CodexCliClient>.Instance),
            options,
            CodingHost.Desktop("test"));
    }

    private static IReadOnlyList<string> ExpectedExecArguments(
        CodingStrategyKind kind,
        CodingTaskRequest request,
        string workingDirectory)
        => kind switch
        {
            CodingStrategyKind.Codex => new CodexCommandBuilder(new CodexCliOptions()).BuildExecArguments(
                new CodexExecRequest
                {
                    Prompt = request.Prompt,
                    WorkingDirectory = workingDirectory
                },
                workingDirectory),
            CodingStrategyKind.Claude => new ClaudeCommandBuilder(new ClaudeCliOptions()).BuildPrintArguments(request),
            CodingStrategyKind.GrokBuild => new GrokBuildCommandBuilder(new GrokBuildCliOptions())
                .BuildPrintArguments(request, workingDirectory),
            CodingStrategyKind.Cline => new ClineCommandBuilder(new ClineCliOptions { TimeoutSeconds = 20 })
                .BuildTaskArguments(request, workingDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private sealed class TempWorkspace : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("jev-call-ws-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
