using Microsoft.Extensions.Options;

namespace Jev.Core.Persona;

public sealed class JevPersona(IOptions<JevAgentOptions> options)
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
            try
            {
                if (File.Exists(Path.Combine(candidate, SystemFileName)))
                {
                    return candidate;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or NotSupportedException or IOException)
            {
                // WASM / sandboxed hosts cannot probe arbitrary paths.
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
        yield return Path.GetFullPath("prompts");
    }

    private const string EmbeddedSystem = """
        You are Jev, a coding-assistant persona in front of a selectable coding worker (Codex, Claude Code, Grok Build, or Cline).
        Plan and explain. Delegate file and tool work to the selected strategy. Never invent command output.
        Default to workspace-write. Refuse destructive shell actions. Never echo secrets.
        """;

    private const string EmbeddedPolicies = """
        Use probe_coding_worker for availability questions.
        Use scaffold_hello_console for the hello-console demo.
        Use run_coding_task for other implementation work.
        If the selected worker is missing or not supported on this host, say so clearly and include the intended command.
        """;
}
