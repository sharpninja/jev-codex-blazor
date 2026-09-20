using Jev.Codex;
using Jev.Core.Agent;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevAgentEndToEndTests
{
    [Fact]
    public async Task Demo_prompt_goes_through_agent_tools_to_codex()
    {
        var codex = new RecordingCodex();
        var status = new JevRunStatus();
        var tools = new JevCodexTools(codex, status);
        var factory = new JevAgentFactory(
            new JevPersona(Options.Create(new JevAgentOptions()), new StubHostEnvironment()),
            tools,
            status,
            Options.Create(new JevAgentOptions { UseFallbackChatClient = true }),
            Options.Create(new OpenAIOptions()),
            NullLoggerFactory.Instance);

        var handle = factory.Create();
        Assert.True(handle.UsesFallbackChatClient);

        var session = await handle.Agent.CreateSessionAsync();
        var response = await handle.Agent.RunAsync(
            "Scaffold a hello console app in a temp workspace",
            session);

        Assert.Single(codex.Prompts);
        Assert.Contains("Hello from Jev", codex.Prompts[0]);
        Assert.Contains("Program.cs", response.Text);
        Assert.Equal(JevPhase.RunningCodex, status.Phase);

        _ = await handle.Agent.RunAsync("Is Codex available?", session);
        Assert.True(codex.Probed);

        var scaffoldAgain = await handle.Agent.RunAsync(
            "Scaffold a hello console app in a temp workspace",
            session);
        Assert.Equal(2, codex.Prompts.Count);
        Assert.Contains("Program.cs", scaffoldAgain.Text);
    }

    private sealed class RecordingCodex : ICodexCli
    {
        public List<string> Prompts { get; } = [];

        public bool Probed { get; private set; }

        public Task<CodexAvailability> ProbeAsync(CancellationToken cancellationToken = default)
        {
            Probed = true;
            return Task.FromResult(new CodexAvailability
            {
                IsInstalled = true,
                Version = "codex-test 0.0",
                ExecutablePath = "codex"
            });
        }

        public Task<CodexExecResult> ExecAsync(
            CodexExecRequest request,
            IProgress<CodexProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(request.Prompt);
            progress?.Report(new CodexProgress("item.started", "fake codex running"));
            return Task.FromResult(new CodexExecResult
            {
                Succeeded = true,
                ExitCode = 0,
                ThreadId = "thread-test",
                FinalMessage = "Created Program.cs",
                CommandLine = "codex exec --json -",
                WorkingDirectory = "/tmp/jev-test",
                ChangedFiles = ["Program.cs"],
                CommandsRun = ["dotnet new console"]
            });
        }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
