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

| Strategy | CLI | Headless contract | Install / auth |
| --- | --- | --- | --- |
| **Codex** | `codex` | `codex exec --json --sandbox workspace-write --ask-for-approval never --skip-git-repo-check --cd <dir> -` (prompt on stdin) | [Codex CLI](https://developers.openai.com/codex/cli). `codex login` or `OPENAI_API_KEY`. |
| **Claude** | `claude` | `claude --bare -p "<prompt>" --output-format json --permission-mode acceptEdits --allowedTools Read,Edit,Bash` (cwd = workspace) | [Claude Code CLI](https://code.claude.com/docs/en/headless). `claude login` or `ANTHROPIC_API_KEY` (`--bare` does not use subscription login). |
| **GrokBuild** | `grok` | `grok -p "<prompt>" --output-format streaming-json --always-approve --cwd <dir>` | [Grok Build](https://x.ai/cli) / [docs](https://docs.x.ai/build/overview). `curl -fsSL https://x.ai/cli/install.sh \| bash`, then `grok login` or `XAI_API_KEY`. |
| **Cline** | `cline` | `cline --json --yolo --auto-approve true --cwd <dir> --timeout <sec> "<prompt>"` | [Cline CLI](https://docs.cline.bot/cli/cli-reference). `npm i -g cline`, then `cline auth` or `-P` / `-k` / provider env vars. |

Each adapter probes with `--version`, captures stdout/stderr, and returns a structured not-installed result (exit 127) when the binary is missing. No fake package versions are invented; the contracts above match current public CLI docs.

## Prerequisites

1. **.NET 10 SDK** (this repo targets `net10.0`; developed against SDK `10.0.401`).
2. **At least one coding-worker CLI** on `PATH` if you want real file edits. The app still runs and chats when none are installed.
3. **Optional OpenAI API key** for the Jev orchestration model. If unset, Jev uses a local fallback router that still emits Agent Framework function calls into the selected strategy.

## Configure and switch

Copy [`.env.example`](.env.example) or use user secrets:

```bash
cd src/Jev.Web
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>"
dotnet user-secrets set "Jev:CodingStrategy" "Claude"
```

| Variable / key | Purpose |
| --- | --- |
| `Jev:CodingStrategy` / `JEV_CODING_STRATEGY` | `Codex` (default), `Claude`, `GrokBuild`, or `Cline` |
| `OPENAI_API_KEY` / `OPENAI_MODEL` | Jev orchestration LLM |
| `CODEX_EXECUTABLE` | Codex binary (default `codex`) |
| `CLAUDE_EXECUTABLE` | Claude Code binary (default `claude`) |
| `GROK_EXECUTABLE` | Grok Build binary (default `grok`) |
| `CLINE_EXECUTABLE` | Cline binary (default `cline`) |
| `ANTHROPIC_API_KEY` | Claude Code / Cline Anthropic provider |
| `XAI_API_KEY` | Grok Build |

In the Blazor sidebar, the four strategies are listed with ready/missing probes. Switching starts a new conversation so history from one worker is not reused with another.

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
2. **Is the coding worker available?** — Agent Framework calls `probe_coding_worker` on the active strategy.
3. **Scaffold a hello console app in a temp workspace** — Agent Framework calls `scaffold_hello_console`, which runs the selected strategy's headless command in a temp workspace.

If the selected CLI is not installed, the tool fails clearly and Jev reports the intended command — it does not invent files.

## Architecture notes

### Microsoft Agent Framework

Packages (current stable as of this skeleton):

- `Microsoft.Agents.AI` 1.22.0
- `Microsoft.Agents.AI.OpenAI` 1.22.0

Jev is a `ChatClientAgent` created with `IChatClient.AsAIAgent(...)`, `AgentSession` for conversation state, and `AIFunctionFactory` tools. Streaming uses `RunStreamingAsync`. Tools close over `ICodingStrategySelector.Active`, so a sidebar switch takes effect on the next tool call.

### Fallback orchestration

When `OPENAI_API_KEY` is empty, `FallbackJevChatClient` implements `IChatClient` and chooses tools via `JevIntentRouter`. `ChatClientAgent` still wraps it with function invocation.

## License

Use as a starting skeleton. Each worker CLI remains subject to its vendor terms.
