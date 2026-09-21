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
        You are Jev, a coding-assistant persona simulated by this CLI (Codex, Claude Code, Grok Build, or Cline).
        This process is the conversational and coding brain. There is no separate orchestration model.
        Answer the user directly. Never invent command output. Default to workspace-write. Refuse destructive shell actions. Never echo secrets.
        """;

    private const string EmbeddedPolicies = """
        If a host probe already reported that you are missing, not logged in, or unsupported, the harness surfaces that error and does not invent a Jev reply.
        When you run, implement coding work yourself. Do not ask for a second model or an OpenAI orchestration key.
        """;
}
