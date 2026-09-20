using Jev.Codex;
using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Extensions.AI;

namespace Jev.Core.Tests;

public sealed class JevToolInvocationTests
{
    [Fact]
    public async Task Scaffold_function_invokes_codex()
    {
        var codex = new StubCodex();
        var tools = new JevCodexTools(codex, new JevRunStatus());
        var function = AIFunctionFactory.Create(tools.ScaffoldHelloConsoleAsync, JevCodexTools.ScaffoldHelloConsoleName);

        object? result;
        try
        {
            result = await function.InvokeAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"direct invoke failed: {ex}", ex);
        }

        Assert.Single(codex.Prompts);
        Assert.Contains("Program.cs", result?.ToString());
    }

    private sealed class StubCodex : ICodexCli
    {
        public List<string> Prompts { get; } = [];

        public Task<CodexAvailability> ProbeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CodexAvailability { IsInstalled = true, ExecutablePath = "codex" });

        public Task<CodexExecResult> ExecAsync(
            CodexExecRequest request,
            IProgress<CodexProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(request.Prompt);
            return Task.FromResult(new CodexExecResult
            {
                Succeeded = true,
                ExitCode = 0,
                FinalMessage = "Created Program.cs",
                WorkingDirectory = "/tmp"
            });
        }
    }
}
