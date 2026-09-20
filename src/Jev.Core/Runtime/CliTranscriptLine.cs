namespace Jev.Core.Runtime;

public sealed record CliTranscriptLine(
    DateTimeOffset Timestamp,
    CliTranscriptChannel Channel,
    string Text);
