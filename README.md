# Jev + coding-worker strategies + Blazor

A .NET 10 solution where a user chats with **Jev** in a Blazor harness. Jev is a coding-assistant *emulation layer* (persona, policies, and tool wrappers). Microsoft Agent Framework owns conversation and tool orchestration. A **strategy-pattern coding worker** does the actual file/tool work.

```
Blazor chat UI
    → Jev ChatClientAgent (Microsoft Agent Framework)
        → Jev tools (probe / run task / scaffold demo)
            → selected ICodingAgentStrategy
                → Codex | Claude Code | Grok Build | Cline
```

The UI never talks to a CLI directly. Switching the active strategy (config, `JEV_CODING_STRATEGY`, or the sidebar) changes which worker the tools call.

**Auth split:** coding workers use **subscription login only**. An optional OpenAI key, if present, is only for Jev's orchestration LLM — never for Codex, Claude, Grok, or Cline.

## Who is Jev?

This repo does not consume an external "Jev" product spec. **Jev** here is a local coding-assistant persona:

- Plans first, then delegates implementation.
- Explains results, diffs, and leftover risk in a calm senior-engineer voice.
- Refuses destructive shell actions and never echoes secrets.
- Treats the selected coding worker as the process that creates and edits files.

Persona and policy text live in editable markdown:

- [`prompts/jev-system.md`](prompts/jev-system.md)
- [`prompts/jev-policies.md`](prompts/jev-policies.md)

## Projects

| Project | Role |
| --- | --- |
| `src/Jev.Web` | Blazor Web App (Interactive Server) chat harness |
| `src/Jev.Core` | Jev persona, Agent Framework agent, strategy-aware tools, fallback router |
| `src/Jev.Workers` | `ICodingAgentStrategy`, factory/selector, Claude / Grok Build / Cline adapters |
| `src/Jev.Codex` | Codex CLI process adapter and JSONL parser (used by the Codex strategy) |
| `tests/*` | Command contracts, routing, selector, and adapter tests |

## Coding-worker strategies

Each worker authenticates with a **vendor subscription login**. This repo does not require or prefer API keys for these CLIs.

| Strategy | CLI | Headless contract | Subscription login | Session probe |
| --- | --- | --- | --- | --- |
| **Codex** | `codex` | `codex exec --json --sandbox workspace-write --ask-for-approval never --skip-git-repo-check --cd <dir> -` (prompt on stdin) | [Codex CLI](https://developers.openai.com/codex/cli): `codex login` (ChatGPT). Do not use `OPENAI_API_KEY` for Codex. | `codex login status` (exit 0 when signed in) |
| **Claude** | `claude` | `claude -p "<prompt>" --output-format json --permission-mode acceptEdits --allowedTools Read,Edit,Bash` (cwd = workspace) | [Claude Code](https://code.claude.com/docs/en/authentication): `claude auth login` (Claude Pro/Max/Team/Enterprise). Do **not** pass `--bare` — that skips OAuth and wants an API key. | `claude auth status` (exit 0 when signed in) |
| **GrokBuild** | `grok` | `grok -p "<prompt>" --output-format streaming-json --always-approve --cwd <dir>` | [Grok Build](https://docs.x.ai/build/cli/reference): `grok login` (SuperGrok / X Premium+). Headless: `grok login --device-auth`. | No public status command — run `grok login` once. |
| **Cline** | `cline` | `cline --json --yolo --auto-approve true --cwd <dir> --timeout <sec> "<prompt>"` | [Cline CLI](https://docs.cline.bot/getting-started/authorizing-with-cline): `cline auth` (or `cline a`) and choose **Sign in with Cline** / ClinePass. Default provider is `cline` (subscription). | No public status command — run `cline auth` once; `cline config` inspects the saved session. |

Probes first run `--version`. When the CLI exposes a login-status command, a failed status is reported as **not logged in** (separate from **not installed**). Run failures that look like auth errors point at the same login command. No fake package versions are invented; the contracts above match current public CLI docs.

## Prerequisites

1. **.NET 10 SDK** (this repo targets `net10.0`; developed against SDK `10.0.401`).
2. **At least one coding-worker CLI** on `PATH`, then its **subscription login** (table above), if you want real file edits. The app still runs and chats when none are installed.
3. **Optional orchestration LLM** for Jev's Agent Framework chat client (`OpenAI:ApiKey` / `OPENAI_API_KEY`). If unset, Jev uses a local fallback router that still emits function calls into the selected strategy. This key is **not** used by the coding workers.

## Configure and switch

Copy [`.env.example`](.env.example) or use user secrets:

```bash
cd src/Jev.Web
# Optional — Jev orchestration LLM only. Not used by coding workers.
dotnet user-secrets set "OpenAI:ApiKey" "<orchestration-key>"
dotnet user-secrets set "Jev:CodingStrategy" "Claude"
```

Then sign the worker in from a terminal (once per machine):

```bash
codex login           # ChatGPT subscription
claude auth login     # Claude subscription
grok login            # SuperGrok / X Premium+
cline auth            # Cline / ClinePass
```

| Variable / key | Purpose |
| --- | --- |
| `Jev:CodingStrategy` / `JEV_CODING_STRATEGY` | `Codex` (default), `Claude`, `GrokBuild`, or `Cline` |
| `OPENAI_API_KEY` / `OPENAI_MODEL` | Optional Jev **orchestration** LLM only |
| `CODEX_EXECUTABLE` | Codex binary (default `codex`) |
| `CLAUDE_EXECUTABLE` | Claude Code binary (default `claude`) |
| `GROK_EXECUTABLE` | Grok Build binary (default `grok`) |
| `CLINE_EXECUTABLE` | Cline binary (default `cline`) |

In the Blazor sidebar, the four strategies are listed with missing / login / ready badges. Tooltips show the subscription login command. Switching starts a new conversation so history from one worker is not reused with another.

Do not commit secrets. `appsettings.json` only has empty placeholders.

## Run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Jev.Web
```

Open the printed HTTP URL (launch profile defaults to `http://localhost:5296`).

## Try a sample chat

1. **Who are you?** — Jev answers from the persona layer. No worker process.
2. **Is the coding worker available?** — Agent Framework calls `probe_coding_worker` on the active strategy (install + login when the CLI exposes it).
3. **Scaffold a hello console app in a temp workspace** — Agent Framework calls `scaffold_hello_console`, which runs the selected strategy's headless command in a temp workspace.

If the selected CLI is not installed or not logged in, the tool fails clearly and Jev reports the intended command — it does not invent files.

## Architecture notes

### Microsoft Agent Framework

Packages (current stable as of this skeleton):

- `Microsoft.Agents.AI` 1.22.0
- `Microsoft.Agents.AI.OpenAI` 1.22.0

Jev is a `ChatClientAgent` created with `IChatClient.AsAIAgent(...)`, `AgentSession` for conversation state, and `AIFunctionFactory` tools. Streaming uses `RunStreamingAsync`. Tools close over `ICodingStrategySelector.Active`, so a sidebar switch takes effect on the next tool call.

### Fallback orchestration

When the optional orchestration key is empty, `FallbackJevChatClient` implements `IChatClient` and chooses tools via `JevIntentRouter`. `ChatClientAgent` still wraps it with function invocation. Coding-worker auth is unchanged: subscription login on the selected CLI.

## License

Use as a starting skeleton. Each worker CLI remains subject to its vendor terms.
