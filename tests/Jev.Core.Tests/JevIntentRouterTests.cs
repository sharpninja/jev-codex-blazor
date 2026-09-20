using Jev.Core.Routing;

namespace Jev.Core.Tests;

public sealed class JevIntentRouterTests
{
    [Theory]
    [InlineData("Is Codex available?", JevToolChoice.ProbeWorker)]
    [InlineData("Is the coding worker available?", JevToolChoice.ProbeWorker)]
    [InlineData("Is Claude available?", JevToolChoice.ProbeWorker)]
    [InlineData("scaffold a hello console app in a temp workspace", JevToolChoice.ScaffoldHelloConsole)]
    [InlineData("create a file in this workspace", JevToolChoice.RunCodingTask)]
    [InlineData("Who are you?", JevToolChoice.None)]
    public void Routes_expected_intents(string message, JevToolChoice expected)
        => Assert.Equal(expected, JevIntentRouter.Choose(message));
}
