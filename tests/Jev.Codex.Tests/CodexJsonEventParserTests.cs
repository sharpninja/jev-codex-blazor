using Jev.Codex;

namespace Jev.Codex.Tests;

public sealed class CodexJsonEventParserTests
{
    [Fact]
    public void Parses_thread_and_agent_message_events()
    {
        var started = CodexJsonEventParser.TryParse("""{"type":"thread.started","thread_id":"abc"}""");
        var message = CodexJsonEventParser.TryParse(
            """{"type":"item.completed","item":{"id":"item_3","type":"agent_message","text":"Created Program.cs"}}""");
        var command = CodexJsonEventParser.TryParse(
            """{"type":"item.started","item":{"id":"item_1","type":"command_execution","command":"dotnet new console"}}""");
        var files = CodexJsonEventParser.TryParse(
            """{"type":"item.completed","item":{"type":"file_change","changes":[{"path":"Program.cs","kind":"add"}]}}""");

        Assert.Equal("abc", started?.ThreadId);
        Assert.Equal("Created Program.cs", message?.Text);
        Assert.True(message?.IsAgentMessage);

        var events = new[] { started!, command!, files!, message! };
        Assert.Equal("abc", CodexJsonEventParser.FirstThreadId(events));
        Assert.Equal("Created Program.cs", CodexJsonEventParser.LastAgentMessage(events));
        Assert.Equal(["Program.cs"], CodexJsonEventParser.DistinctChangedFiles(events));
        Assert.Equal(["dotnet new console"], CodexJsonEventParser.CommandsRun(events));
    }

    [Fact]
    public void Ignores_non_json_and_extracts_errors()
    {
        Assert.Null(CodexJsonEventParser.TryParse("not json"));
        var failed = CodexJsonEventParser.TryParse(
            """{"type":"turn.failed","error":{"message":"sandbox denied"}}""");
        Assert.Equal("sandbox denied", CodexJsonEventParser.FirstError([failed!]));
    }
}
