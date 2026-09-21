using Jev.Codex;
using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Jev.Workers;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevCliSimulatorProcessTests
{
    [Fact]
    public async Task Who_are_you_runs_a_real_cli_stub_and_never_uses_the_fallback_router()
    {
        using var dir = new TempDir();
        var stub = OperatingSystem.IsWindows()
            ? dir.WriteExecutable("codex.cmd", """
                @echo off
                if "%~1"=="--version" (
                    echo codex-stub 0.0
                    exit /b 0
                )
                if "%~1"=="login" exit /b 0
                more >nul
                echo {"type":"thread.started","thread_id":"stub-thread"}
                echo {"type":"item.completed","item":{"type":"agent_message","text":"I am Jev, simulated by the Codex CLI stub. There is no fallback router."}}
                exit /b 0
                """)
            : dir.WriteExecutable("codex", """
            #!/bin/sh
            set -e
            if [ "$1" = "--version" ]; then
              echo "codex-stub 0.0"
              exit 0
            fi
            if [ "$1" = "login" ]; then
              exit 0
            fi
            cat >/dev/null
            echo '{"type":"thread.started","thread_id":"stub-thread"}'
            echo '{"type":"item.completed","item":{"type":"agent_message","text":"I am Jev, simulated by the Codex CLI stub. There is no fallback router."}}'
            """);

        var options = Options.Create(new CodexCliOptions
        {
            ExecutablePath = stub,
            TimeoutSeconds = 15
        });
        var worker = new CodexCodingStrategy(
            new CodexCliClient(options, new CodexProcessRunner(), NullLogger<CodexCliClient>.Instance),
            options,
            CodingHost.Desktop("test"));
        var status = new JevRunStatus();
        var simulator = new JevCliSimulator(
            new JevPersona(Options.Create(new JevAgentOptions())),
            new FixedSelector(worker),
            status,
            new CliTranscript());

        var result = await simulator.SendAsync("who are you?");

        Assert.True(result.Succeeded, result.Text);
        Assert.Equal("I am Jev, simulated by the Codex CLI stub. There is no fallback router.", result.Text);
        Assert.DoesNotContain("This turn is using the local fallback router", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("no orchestration LLM key", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Codex CLI", status.Simulator);
        Assert.Equal("stub-thread", simulator.SessionId);
    }

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
