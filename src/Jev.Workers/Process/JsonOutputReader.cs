using System.Text.Json;

namespace Jev.Workers.Process;

public static class JsonOutputReader
{
    public static (string? Text, string? SessionId) Extract(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return (null, null);
        }

        string? text = null;
        string? session = null;

        foreach (var line in stdout.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] != '{')
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;
                text = ReadString(root, "result")
                    ?? ReadString(root, "text")
                    ?? ReadString(root, "message")
                    ?? ReadString(root, "final_message")
                    ?? text;
                session = ReadString(root, "session_id")
                    ?? ReadString(root, "sessionId")
                    ?? ReadString(root, "thread_id")
                    ?? session;
            }
            catch (JsonException)
            {
                // ignore non-JSON lines
            }
        }

        if (text is null)
        {
            var trimmed = stdout.Trim();
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                try
                {
                    using var document = JsonDocument.Parse(trimmed);
                    text = ReadString(document.RootElement, "result")
                        ?? ReadString(document.RootElement, "text");
                    session ??= ReadString(document.RootElement, "session_id");
                }
                catch (JsonException)
                {
                    text = trimmed;
                }
            }
            else
            {
                text = trimmed;
            }
        }

        return (text, session);
    }

    private static string? ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
