namespace Jev.Workers.Cli;

public sealed class GrokBuildCommandBuilder(GrokBuildCliOptions options)
{
    public IReadOnlyList<string> BuildVersionArguments() => ["--version"];

    public IReadOnlyList<string> BuildPrintArguments(CodingTaskRequest request, string workingDirectory)
    {
        var args = new List<string>
        {
            "-p",
            request.Prompt,
            "--output-format",
            options.OutputFormat,
            "--cwd",
            workingDirectory
        };

        if (options.AlwaysApprove)
        {
            args.Add("--always-approve");
        }

        var model = request.Model ?? options.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Add("-m");
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
