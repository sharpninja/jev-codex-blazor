using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Extensions.AI;

namespace Jev.Core.Tests;

public sealed class JevToolInvocationTests
{
    [Fact]
    public async Task Scaffold_function_invokes_selected_strategy()
    {
        var worker = new RecordingStrategy();
        var tools = new JevCodingTools(new FixedSelector(worker), new JevRunStatus());
        var function = AIFunctionFactory.Create(tools.ScaffoldHelloConsoleAsync, JevCodingTools.ScaffoldHelloConsoleName);

        var result = await function.InvokeAsync();

        Assert.Single(worker.Prompts);
        Assert.Contains("Program.cs", result?.ToString());
    }
}
