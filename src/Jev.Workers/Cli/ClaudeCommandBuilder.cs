namespace Jev.Workers.Cli;

public sealed class ClaudeCommandBuilder(ClaudeCliOptions options)
{
    public IReadOnlyList<string> BuildVersionArguments() => ["--version"];

    public IReadOnlyList<string> BuildAuthStatusArguments() => ["auth", "status"];

    public IReadOnlyList<string> BuildPrintArguments(CodingTaskRequest request)
    {
        // Headless print uses the stored Claude subscription session from
        // `claude auth login`. Do not pass --bare: that skips OAuth and wants an API key.
        var args = new List<string>
        {
            "-p",
            request.Prompt
        };
        args.Add("--output-format");
        args.Add(options.OutputFormat);

        if (!string.IsNullOrWhiteSpace(options.PermissionMode))
        {
            args.Add("--permission-mode");
            args.Add(options.PermissionMode);
        }

        if (!string.IsNullOrWhiteSpace(options.AllowedTools))
        {
            args.Add("--allowedTools");
            args.Add(options.AllowedTools);
        }

        var model = request.Model ?? options.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Add("--model");
            args.Add(model);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            args.Add("--resume");
            args.Add(request.SessionId);
        }

        return args;
    }
}
