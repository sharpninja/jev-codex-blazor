namespace Jev.Codex;

public sealed record CodexProgress(
    string Phase,
    string Message,
    CodexJsonEvent? Event = null);
