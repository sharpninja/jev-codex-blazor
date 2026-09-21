using System.Text;
using Jev.Core;
using Jev.Core.Persona;
using Jev.Core.Simulation;
using Microsoft.Extensions.Options;

namespace Jev.Core.Tests;

public sealed class JevPersonaUtf8Tests
{
    [Fact]
    public void Persona_markdown_keeps_em_dashes_and_encodes_as_utf8()
    {
        var prompts = FindPromptsDirectory();
        foreach (var name in new[] { JevPersona.SystemFileName, JevPersona.PoliciesFileName })
        {
            var bytes = File.ReadAllBytes(Path.Combine(prompts, name));
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            Assert.Contains("—", text, StringComparison.Ordinal);
        }

        var persona = new JevPersona(Options.Create(new JevAgentOptions { PersonaDirectory = prompts }));
        var instructions = persona.LoadInstructions();
        Assert.Contains("—", instructions, StringComparison.Ordinal);

        var prompt = JevPromptBuilder.Build(instructions, "Codex", [], "Who are you?");
        var encoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(prompt);
        Assert.Equal(prompt, new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(encoded));
        Assert.True(encoded.AsSpan().IndexOf("—"u8) >= 0);
    }

    private static string FindPromptsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var prompts = Path.Combine(dir.FullName, "prompts");
            if (File.Exists(Path.Combine(prompts, JevPersona.SystemFileName)))
            {
                return prompts;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find prompts/jev-system.md from the test output directory.");
    }
}
