namespace Jev.Codex;

public sealed class CodexCommandBuilder(CodexCliOptions options)
{
    public IReadOnlyList<string> BuildExecArguments(CodexExecRequest request, string workingDirectory)
    {
        var args = new List<string> { "exec" };

        if (options.UseJsonEvents)
        {
            args.Add("--json");
        }

        args.Add("--color");
        args.Add("never");

        var sandbox = CodexSandbox.Clamp(
            CodexSandbox.Normalize(request.Sandbox, options.DefaultSandbox),
            options.AllowDangerousSandbox);
        args.Add("--sandbox");
        args.Add(sandbox);

        if (!string.IsNullOrWhiteSpace(options.AskForApproval))
        {
            args.Add("--ask-for-approval");
            args.Add(options.AskForApproval);
        }

        if (options.SkipGitRepoCheck)
        {
            args.Add("--skip-git-repo-check");
        }

        args.Add("--cd");
        args.Add(workingDirectory);

        var model = request.Model ?? options.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Add("--model");
            args.Add(model);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            args.Add("resume");
            args.Add(request.SessionId);
        }

        // "-" tells `codex exec` to read the prompt from stdin.
        args.Add("-");
        return args;
    }

    public IReadOnlyList<string> BuildVersionArguments() => ["--version"];

    public static string FormatCommandLine(string executable, IEnumerable<string> arguments)
        => string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote));

    private static string Quote(string value)
        => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
