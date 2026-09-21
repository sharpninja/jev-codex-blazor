using Jev.Codex;
using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Jev.Workers;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevCliSimulatorProcessTests
{
    [Fact]
    public Task Who_are_you_runs_a_real_codex_stub_and_never_uses_the_fallback_router()
        => WhoAreYouAsync(CodingStrategyKind.Codex);

    [Fact]
    public Task Who_are_you_runs_a_real_claude_stub()
        => WhoAreYouAsync(CodingStrategyKind.Claude);

    [Fact]
    public Task Who_are_you_runs_a_real_grok_build_stub()
        => WhoAreYouAsync(CodingStrategyKind.GrokBuild);

    [Fact]
    public Task Who_are_you_runs_a_real_cline_stub()
        => WhoAreYouAsync(CodingStrategyKind.Cline);

    private static async Task WhoAreYouAsync(CodingStrategyKind kind)
    {
        using var dir = new TempDir();
        var stub = WriteHarnessStub(dir, kind);
        var worker = CreateStrategy(kind, stub, dir.Path);
        var status = new JevRunStatus();
        var simulator = new JevCliSimulator(
            new JevPersona(Options.Create(new JevAgentOptions())),
            new FixedSelector(worker),
            status,
            new CliTranscript());

        var result = await simulator.SendAsync("who are you?");

        Assert.True(result.Succeeded, result.Text);
        Assert.Equal(Marker(kind), result.Text);
        Assert.DoesNotContain("This turn is using the local fallback router", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("no orchestration LLM key", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"{worker.DisplayName} CLI", status.Simulator);
        Assert.Equal(Session(kind), simulator.SessionId);
    }

    private static ICodingAgentStrategy CreateStrategy(CodingStrategyKind kind, string stub, string searchPath)
    {
        var env = new CliSearchEnvironment
        {
            Path = searchPath,
            PathExt = OperatingSystem.IsWindows() ? ".COM;.EXE;.BAT;.CMD;.PS1" : null,
            IsWindows = OperatingSystem.IsWindows(),
            IncludeWellKnownDirectories = false,
            QueryNpmPrefix = false
        };
        return kind switch
        {
            CodingStrategyKind.Codex => CreateCodex(stub, env),
            CodingStrategyKind.Claude => new ClaudeCodingStrategy(
                Options.Create(new ClaudeCliOptions { ExecutablePath = stub, TimeoutSeconds = 15 }),
                new CliProcessRunner(env),
                NullLogger<ClaudeCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            CodingStrategyKind.GrokBuild => new GrokBuildCodingStrategy(
                Options.Create(new GrokBuildCliOptions { ExecutablePath = stub, TimeoutSeconds = 15 }),
                new CliProcessRunner(env),
                NullLogger<GrokBuildCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            CodingStrategyKind.Cline => new ClineCodingStrategy(
                Options.Create(new ClineCliOptions { ExecutablePath = stub, TimeoutSeconds = 15 }),
                new CliProcessRunner(env),
                NullLogger<ClineCodingStrategy>.Instance,
                CodingHost.Desktop("test")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static CodexCodingStrategy CreateCodex(string stub, CliSearchEnvironment env)
    {
        var options = Options.Create(new CodexCliOptions
        {
            ExecutablePath = stub,
            TimeoutSeconds = 15
        });
        return new CodexCodingStrategy(
            new CodexCliClient(options, new CodexProcessRunner(env), NullLogger<CodexCliClient>.Instance),
            options,
            CodingHost.Desktop("test"));
    }

    private static string WriteHarnessStub(TempDir dir, CodingStrategyKind kind)
    {
        var name = kind switch
        {
            CodingStrategyKind.Codex => "codex",
            CodingStrategyKind.Claude => "claude",
            CodingStrategyKind.GrokBuild => "grok",
            CodingStrategyKind.Cline => "cline",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
        var marker = Marker(kind);
        var session = Session(kind);
        var threadJson = "{\"type\":\"thread.started\",\"thread_id\":\"" + session + "\"}";
        var messageJson = "{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"" + marker + "\"}}";
        var resultJson = "{\"result\":\"" + marker + "\",\"session_id\":\"" + session + "\"}";
        var success = kind == CodingStrategyKind.Codex
            ? "echo " + threadJson + Environment.NewLine + "echo " + messageJson
            : "echo " + resultJson;
        var unixSuccess = kind == CodingStrategyKind.Codex
            ? "echo '" + threadJson + "'" + Environment.NewLine + "echo '" + messageJson + "'"
            : "echo '" + resultJson + "'";

        if (OperatingSystem.IsWindows())
        {
            return dir.WriteExecutable(name + ".cmd", $"""
                @echo off
                if "%~1"=="--version" (
                    echo {name}-stub 0.0
                    exit /b 0
                )
                if "%~1"=="version" (
                    echo {name}-stub 0.0
                    exit /b 0
                )
                if "%~1"=="-V" (
                    echo {name}-stub 0.0
                    exit /b 0
                )
                if "%~1"=="login" exit /b 0
                if "%~1"=="auth" exit /b 0
                more >nul
                {success}
                exit /b 0
                """);
        }

        return dir.WriteExecutable(name, $"""
            #!/bin/sh
            set -e
            if [ "$1" = "--version" ] || [ "$1" = "version" ] || [ "$1" = "-V" ]; then
              echo "{name}-stub 0.0"
              exit 0
            fi
            if [ "$1" = "login" ] || [ "$1" = "auth" ]; then
              exit 0
            fi
            cat >/dev/null
            {unixSuccess}
            """);
    }

    private static string Marker(CodingStrategyKind kind)
        => kind switch
        {
            CodingStrategyKind.Codex => "I am Jev, simulated by the Codex CLI stub. There is no fallback router.",
            CodingStrategyKind.Claude => "I am Jev, simulated by the Claude CLI stub.",
            CodingStrategyKind.GrokBuild => "I am Jev, simulated by the Grok Build CLI stub.",
            CodingStrategyKind.Cline => "I am Jev, simulated by the Cline CLI stub.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string Session(CodingStrategyKind kind)
        => kind switch
        {
            CodingStrategyKind.Codex => "codex-stub-thread",
            CodingStrategyKind.Claude => "claude-stub-session",
            CodingStrategyKind.GrokBuild => "grok-stub-session",
            CodingStrategyKind.Cline => "cline-stub-session",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("jev-sim-").FullName;

        public string WriteExecutable(string name, string contents)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, contents);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    file,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            return file;
        }

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
