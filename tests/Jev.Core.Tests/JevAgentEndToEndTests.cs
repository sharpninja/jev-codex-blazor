using Jev.Core.Agent;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevAgentEndToEndTests
{
    [Fact]
    public async Task Demo_prompt_goes_through_agent_tools_to_selected_strategy()
    {
        var worker = new RecordingStrategy();
        var status = new JevRunStatus();
        var tools = new JevCodingTools(new FixedSelector(worker), status);
        var factory = new JevAgentFactory(
            new JevPersona(Options.Create(new JevAgentOptions()), new StubHostEnvironment()),
            tools,
            new FixedSelector(worker),
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

        Assert.Single(worker.Prompts);
        Assert.Contains("Hello from Jev", worker.Prompts[0]);
        Assert.Contains("Program.cs", response.Text);
        Assert.Equal(JevPhase.RunningWorker, status.Phase);

        _ = await handle.Agent.RunAsync("Is the coding worker available?", session);
        Assert.True(worker.Probed);

        var scaffoldAgain = await handle.Agent.RunAsync(
            "Scaffold a hello console app in a temp workspace",
            session);
        Assert.Equal(2, worker.Prompts.Count);
        Assert.Contains("Program.cs", scaffoldAgain.Text);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
