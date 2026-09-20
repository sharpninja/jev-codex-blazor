# Jev + Codex + Blazor

A .NET 10 solution where a user chats with **Jev** in a Blazor harness. Jev is a coding-assistant *emulation layer* (persona, policies, and tool wrappers). Microsoft Agent Framework owns conversation and tool orchestration. The OpenAI **Codex CLI** does the actual coding work as a subprocess.

```
Blazor chat UI
    → Jev ChatClientAgent (Microsoft Agent Framework)
        → Jev tools (probe / run task / scaffold demo)
            → Codex CLI adapter (`codex exec --json`)
```

The UI never talks to Codex directly.

## Who is Jev?

This repo does not consume an external "Jev" product spec. **Jev** here is a local coding-assistant persona:

- Plans first, then delegates implementation.
- Explains results, diffs, and leftover risk in a calm senior-engineer voice.
- Refuses destructive shell actions and never echoes secrets.
- Treats Codex as the worker that creates and edits files.

Persona and policy text live in editable markdown:

- [`prompts/jev-system.md`](prompts/jev-system.md)
- [`prompts/jev-policies.md`](prompts/jev-policies.md)

`Jev.Core` loads those files at runtime (with a short embedded fallback if they are missing).

## Projects

| Project | Role |
| --- | --- |
| `src/Jev.Web` | Blazor Web App (Interactive Server) chat harness |
| `src/Jev.Core` | Jev persona, Agent Framework agent, Codex tools, fallback router |
| `src/Jev.Codex` | Codex CLI process adapter and JSONL parser |
| `tests/*` | Command contract, JSON parsing, routing, and adapter tests |

## Prerequisites

1. **.NET 10 SDK** (this repo targets `net10.0`; developed against SDK `10.0.401`).
2. **Codex CLI** on `PATH` for real coding tasks:
   - Install from [OpenAI Codex CLI](https://developers.openai.com/codex/cli).
   - Authenticate with `codex login` (ChatGPT) or an API key in the environment (`OPENAI_API_KEY`).
   - Confirm with `codex --version`.
3. **Optional OpenAI API key** for the Jev orchestration model (the Agent Framework chat client). If this is unset, Jev uses a local fallback router that still emits Agent Framework function calls into the Codex tools.

## Configure

Copy [`.env.example`](.env.example) and export the values, or use .NET user secrets (preferred for keys):

```bash
cd src/Jev.Web
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

Environment variables the app understands:

| Variable | Purpose |
| --- | --- |
| `OPENAI_API_KEY` | Jev orchestration LLM (and typically Codex auth) |
| `OPENAI_MODEL` | Orchestration model (default `gpt-4o-mini`) |
| `CODEX_EXECUTABLE` | Codex binary (default `codex`) |
| `CODEX_DEFAULT_SANDBOX` | `read-only` / `workspace-write` / `danger-full-access` |
| `CODEX_TIMEOUT_SECONDS` | Subprocess timeout (default `300`) |

Do not commit secrets. `appsettings.json` only has empty placeholders.

## Run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Jev.Web
```

Open the printed HTTP URL (launch profile defaults to `http://localhost:5296`). You should see the Jev chat UI with orchestration and Codex status in the left rail.

## Try a sample chat

1. **Who are you?** — Jev answers from the persona layer. No Codex process.
2. **Is Codex available?** — Agent Framework calls `probe_codex`.
3. **Scaffold a hello console app in a temp workspace** — Agent Framework calls `scaffold_hello_console`, which runs:

   ```text
   codex exec --json --color never --sandbox workspace-write \
     --ask-for-approval never --skip-git-repo-check --cd <temp-workspace> -
   ```

   The prompt is written to stdin. The adapter parses JSONL events (`thread.started`, `item.completed` / `agent_message`, `file_change`, `command_execution`, `error`) and returns a structured result to Jev, who replies in the chat.

If Codex is not installed, the tool fails clearly (exit 127 / not-found message) and Jev reports that — it does not invent files.

## Architecture notes

### Microsoft Agent Framework

Packages (current stable as of this skeleton):

- `Microsoft.Agents.AI` 1.22.0
- `Microsoft.Agents.AI.OpenAI` 1.22.0

Jev is a `ChatClientAgent` created with `IChatClient.AsAIAgent(...)`, `AgentSession` for conversation state, and `AIFunctionFactory` tools. Streaming uses `RunStreamingAsync`.

If APIs differ from older "AgentThread" samples: this repo uses the current **session** APIs (`CreateSessionAsync` / `RunStreamingAsync(message, session)`), not `GetNewThread`.

### Codex CLI contract

`Jev.Codex.CodexCliClient` is the only process integration:

- Probe: `codex --version`
- Work: `codex exec --json ... -` with the prompt on stdin
- Optional resume: `codex exec ... resume <thread-id> -`
- Sandbox is clamped away from `danger-full-access` unless `Codex:AllowDangerousSandbox` is true
- Missing binary → `CodexNotInstalledException` / structured failed result

### Fallback orchestration

When `OPENAI_API_KEY` is empty, `FallbackJevChatClient` implements `IChatClient` and chooses tools via `JevIntentRouter`. `ChatClientAgent` still wraps it with function invocation, so the Blazor → agent → Codex path stays intact without a remote LLM.

## License

Use as a starting skeleton. Codex CLI and model access remain subject to OpenAI terms.
