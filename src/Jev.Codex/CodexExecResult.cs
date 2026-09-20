namespace Jev.Codex;

public sealed class CodexExecResult
{
    public required bool Succeeded { get; init; }

    public int ExitCode { get; init; }

    public string? ThreadId { get; init; }

    public string? FinalMessage { get; init; }

    public string? Error { get; init; }

    public string CommandLine { get; init; } = "";

    public string WorkingDirectory { get; init; } = "";

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    public IReadOnlyList<string> CommandsRun { get; init; } = [];

    public string Stderr { get; init; } = "";

    public IReadOnlyList<CodexJsonEvent> Events { get; init; } = [];

    public string FormatForAgent()
    {
        var lines = new List<string>
        {
            Succeeded ? "Codex finished successfully." : "Codex did not complete successfully.",
            $"exit_code: {ExitCode}",
            $"working_directory: {WorkingDirectory}",
            $"command: {CommandLine}"
        };

        if (!string.IsNullOrWhiteSpace(ThreadId))
        {
            lines.Add($"codex_thread_id: {ThreadId}");
        }

        if (ChangedFiles.Count > 0)
        {
            lines.Add("changed_files:");
            lines.AddRange(ChangedFiles.Select(path => $"- {path}"));
        }

        if (CommandsRun.Count > 0)
        {
            lines.Add("commands_run:");
            lines.AddRange(CommandsRun.Select(command => $"- {command}"));
        }

        if (!string.IsNullOrWhiteSpace(FinalMessage))
        {
            lines.Add("final_message:");
            lines.Add(FinalMessage);
        }

        if (!string.IsNullOrWhiteSpace(Error))
        {
            lines.Add("error:");
            lines.Add(Error);
        }

        if (!Succeeded && !string.IsNullOrWhiteSpace(Stderr))
        {
            lines.Add("stderr:");
            lines.Add(TrimForAgent(Stderr));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string TrimForAgent(string text, int max = 4000)
        => text.Length <= max ? text : text[..max] + $"{Environment.NewLine}... truncated ...";
}
