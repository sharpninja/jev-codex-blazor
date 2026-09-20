using Jev.Workers;
using Jev.Workers.Process;

namespace Jev.Core.Runtime;

public sealed class CliTranscript : ICliTranscript
{
    public const int MaxLines = 500;

    private readonly List<CliTranscriptLine> _lines = [];
    private readonly object _gate = new();

    public IReadOnlyList<CliTranscriptLine> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    public event EventHandler? Changed;

    public void Append(CliTranscriptChannel channel, string text)
    {
        var sanitized = SecretSanitizer.Redact(text);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return;
        }

        lock (_gate)
        {
            _lines.Add(new CliTranscriptLine(DateTimeOffset.UtcNow, channel, sanitized));
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Append(CodingProgress progress)
    {
        var channel = progress.Phase switch
        {
            CodingProgress.Input => CliTranscriptChannel.Input,
            CodingProgress.Stdout => CliTranscriptChannel.Stdout,
            CodingProgress.Stderr => CliTranscriptChannel.Stderr,
            _ => CliTranscriptChannel.System
        };

        Append(channel, progress.Message);
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_lines.Count == 0)
            {
                return;
            }

            _lines.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
