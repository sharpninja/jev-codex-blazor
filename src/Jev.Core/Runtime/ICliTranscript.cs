using Jev.Workers;

namespace Jev.Core.Runtime;

/// <summary>
/// Scoped, in-memory bus of sanitized CLI input/output for the shared chat UI.
/// </summary>
public interface ICliTranscript
{
    IReadOnlyList<CliTranscriptLine> Lines { get; }

    event EventHandler? Changed;

    void Append(CliTranscriptChannel channel, string text);

    void Append(CodingProgress progress);

    void Clear();

    /// <summary>
    /// Formats currently stored lines the way the CLI drawer shows them
    /// (already-redacted channel + text), for a one-click clipboard copy.
    /// </summary>
    string FormatForClipboard();
}
