using System.Text.Json;

namespace Jev.Codex;

public static class CodexJsonEventParser
{
    public static CodexJsonEvent? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var trimmed = line.Trim();
        if (trimmed[0] != '{')
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeElement))
            {
                return null;
            }

            var type = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            string? itemType = null;
            string? itemId = null;
            string? text = null;
            string? command = null;
            string? itemError = null;
            var changed = new List<string>();

            if (root.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
            {
                itemType = ReadString(item, "type");
                itemId = ReadString(item, "id");
                text = ReadString(item, "text") ?? ReadString(item, "message");
                command = ReadString(item, "command");
                itemError = ReadError(item);
                CollectPaths(item, changed);
            }

            return new CodexJsonEvent
            {
                Type = type,
                ThreadId = ReadString(root, "thread_id"),
                ItemType = itemType,
                ItemId = itemId,
                Text = text,
                Command = command,
                Error = ReadError(root) ?? itemError,
                ChangedPaths = changed,
                Raw = trimmed
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? LastAgentMessage(IEnumerable<CodexJsonEvent> events)
        => events
            .Where(e => e.IsAgentMessage && !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => e.Text)
            .LastOrDefault();

    public static string? FirstThreadId(IEnumerable<CodexJsonEvent> events)
        => events.Select(e => e.ThreadId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

    public static IReadOnlyList<string> DistinctChangedFiles(IEnumerable<CodexJsonEvent> events)
        => events.SelectMany(e => e.ChangedPaths).Distinct(StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<string> CommandsRun(IEnumerable<CodexJsonEvent> events)
        => events
            .Where(e => string.Equals(e.ItemType, "command_execution", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Command)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string? FirstError(IEnumerable<CodexJsonEvent> events)
        => events
            .Where(e =>
                string.Equals(e.Type, "error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Type, "turn.failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.ItemType, "error", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Error ?? e.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    private static string? ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadError(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("error", out var error))
        {
            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (error.ValueKind == JsonValueKind.Object)
            {
                return ReadString(error, "message") ?? ReadString(error, "text") ?? error.GetRawText();
            }
        }

        return ReadString(element, "message");
    }

    private static void CollectPaths(JsonElement item, List<string> changed)
    {
        if (item.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
        {
            foreach (var change in changes.EnumerateArray())
            {
                var path = ReadString(change, "path") ?? ReadString(change, "file");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    changed.Add(path);
                }
            }
        }

        var single = ReadString(item, "path") ?? ReadString(item, "file");
        if (!string.IsNullOrWhiteSpace(single))
        {
            changed.Add(single);
        }
    }
}
