using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Jev.Core.Persona;

public sealed class JevPersona(IOptions<JevAgentOptions> options, IHostEnvironment hostEnvironment)
{
    public const string SystemFileName = "jev-system.md";
    public const string PoliciesFileName = "jev-policies.md";

    public string LoadInstructions()
    {
        var system = ReadPersonaFile(SystemFileName) ?? EmbeddedSystem;
        var policies = ReadPersonaFile(PoliciesFileName) ?? EmbeddedPolicies;
        return system.Trim() + Environment.NewLine + Environment.NewLine + policies.Trim();
    }

    public string? ResolvedDirectory => ResolveDirectory();

    private string? ReadPersonaFile(string fileName)
    {
        var directory = ResolveDirectory();
        if (directory is null)
        {
            return null;
        }

        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private string? ResolveDirectory()
    {
        foreach (var candidate in CandidateDirectories())
        {
            if (File.Exists(Path.Combine(candidate, SystemFileName)))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<string> CandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.PersonaDirectory))
        {
            yield return Path.GetFullPath(options.Value.PersonaDirectory);
        }

        yield return Path.Combine(AppContext.BaseDirectory, "prompts");
        yield return Path.Combine(hostEnvironment.ContentRootPath, "prompts");
        yield return Path.Combine(hostEnvironment.ContentRootPath, "..", "..", "prompts");
        yield return Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, "..", "..", "..", "prompts"));
    }

    private const string EmbeddedSystem = """
        You are Jev, a coding-assistant persona that sits in front of the OpenAI Codex CLI.
        Plan and explain. Delegate file and tool work to Codex tools. Never invent command output.
        Default to workspace-write. Refuse destructive shell actions. Never echo secrets.
        """;

    private const string EmbeddedPolicies = """
        Use probe_codex for availability questions.
        Use scaffold_hello_console for the hello-console demo.
        Use run_coding_task for other implementation work.
        If Codex is missing, say so clearly and include the intended command.
        """;
}
