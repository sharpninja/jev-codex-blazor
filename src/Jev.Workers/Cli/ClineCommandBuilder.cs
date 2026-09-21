namespace Jev.Workers.Cli;

public sealed class ClineCommandBuilder(ClineCliOptions options)
{
    /// <summary>
    /// Current Cline docs verify with <c>cline version</c> (also <c>-V</c> / <c>--version</c>).
    /// </summary>
    public IReadOnlyList<string> BuildVersionArguments() => ["version"];

    public IReadOnlyList<IReadOnlyList<string>> BuildVersionArgumentCandidates()
        => [BuildVersionArguments(), ["--version"], ["-V"]];

    /// <summary>Cline signs in with <c>cline auth</c>; there is no documented non-interactive status command.</summary>
    public IReadOnlyList<string>? BuildAuthStatusArguments() => null;

    public IReadOnlyList<string> BuildTaskArguments(CodingTaskRequest request, string workingDirectory)
    {
        var args = new List<string>();
        if (options.Json)
        {
            args.Add("--json");
        }

        if (options.Yolo)
        {
            args.Add("--yolo");
        }

        if (options.AutoApprove)
        {
            args.Add("--auto-approve");
            args.Add("true");
        }

        args.Add("--cwd");
        args.Add(workingDirectory);

        if (options.TimeoutSeconds > 0)
        {
            args.Add("--timeout");
            args.Add(options.TimeoutSeconds.ToString());
        }

        if (!string.IsNullOrWhiteSpace(options.Provider))
        {
            args.Add("--provider");
            args.Add(options.Provider);
        }

        var model = request.Model ?? options.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Add("--model");
            args.Add(model);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            args.Add("--id");
            args.Add(request.SessionId);
        }

        args.Add(request.Prompt);
        return args;
    }
}
