# Jev + coding-worker strategies + Blazor Hybrid

A .NET 10 solution where a user chats with **Jev** through a **shared Blazor UI** hosted as WASM, ASP.NET, Linux desktop (Photino), and MAUI (Windows / Android). Jev is a coding-assistant *emulation layer*. Microsoft Agent Framework owns conversation and tool orchestration. A **strategy-pattern coding worker** does the actual file/tool work on desktop hosts.

```
Jev.App (shared Razor UI + chat service)
    ├── Jev.Web          ASP.NET Interactive Server (Linux CI / `dotnet run`)
    ├── Jev.Wasm         Blazor WebAssembly (browser)
    ├── Jev.Linux        Photino.Blazor Hybrid (Linux / Windows desktop WebView)
    ├── Jev.Tool         dotnet tool launcher (`jev`) that picks the OS binary
    └── Jev.Maui         .NET MAUI Blazor Hybrid (Windows + Android)
            ↓
    Jev ChatClientAgent (Microsoft Agent Framework)
            ↓
    selected ICodingAgentStrategy
            ↓
    Codex | Claude Code | Grok Build | Cline   (desktop Windows/Linux only)
```

The UI never talks to a CLI directly. Switching the active strategy (config, `JEV_CODING_STRATEGY`, or the sidebar) changes which worker the tools call.

**Auth split:** coding workers use **subscription login only**. An optional OpenAI key, if present, is only for Jev's orchestration LLM — never for Codex, Claude, Grok, or Cline.

**Host split:** local coding CLIs are a desktop/dev-machine concern. WASM and Android report *not supported on this host* instead of crashing. Windows MAUI and Linux Photino still spawn CLIs when they are installed.

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

| Project | Role | In `dotnet build` (Linux CI) |
| --- | --- | --- |
| `src/Jev.App` | Shared Razor class library (chat UI, `JevChatService`) | yes |
| `src/Jev.Web` | Thin Interactive Server host | yes |
| `src/Jev.Wasm` | Blazor WebAssembly host | yes |
| `src/Jev.Linux` | Photino.Blazor Hybrid (linux-x64 + win-x64) | yes |
| `src/Jev.Tool` | `dotnet tool` launcher (`jev`) | yes |
| `src/Jev.Maui` | MAUI Blazor Hybrid (`net10.0-windows10.0.19041.0` + `net10.0-android`) | no — Windows App SDK / Android SDK |
| `src/Jev.Core` | Jev persona, Agent Framework agent, strategy-aware tools | yes |
| `src/Jev.Workers` | `ICodingAgentStrategy`, `ICodingHost`, Claude / Grok / Cline | yes |
| `src/Jev.Codex` | Codex CLI adapter | yes |
| `nuke/` | NUKE 10.1.0 orchestration | yes |
| `tests/*` | Command contracts, selector, host gating | yes |

Official .NET MAUI does not ship a Linux TFM. Linux Hybrid uses [Photino.Blazor 4.0.13](https://www.nuget.org/packages/Photino.Blazor) (WebKitGTK WebView) so the same `Jev.App` UI runs natively on Linux.

## Coding-worker strategies

Each worker authenticates with a **vendor subscription login**. This repo does not require or prefer API keys for these CLIs.

| Strategy | CLI | Headless contract | Subscription login | Session probe |
| --- | --- | --- | --- | --- |
| **Codex** | `codex` | `codex exec --json --sandbox workspace-write --ask-for-approval never --skip-git-repo-check --cd <dir> -` (prompt on stdin) | [Codex CLI](https://developers.openai.com/codex/cli): `codex login` (ChatGPT). Do not use `OPENAI_API_KEY` for Codex. | `codex login status` (exit 0 when signed in) |
| **Claude** | `claude` | `claude -p "<prompt>" --output-format json --permission-mode acceptEdits --allowedTools Read,Edit,Bash` (cwd = workspace) | [Claude Code](https://code.claude.com/docs/en/authentication): `claude auth login` (Claude Pro/Max/Team/Enterprise). Do **not** pass `--bare` — that skips OAuth and wants an API key. | `claude auth status` (exit 0 when signed in) |
| **GrokBuild** | `grok` | `grok -p "<prompt>" --output-format streaming-json --always-approve --cwd <dir>` | [Grok Build](https://docs.x.ai/build/cli/reference): `grok login` (SuperGrok / X Premium+). Headless: `grok login --device-auth`. | No public status command — run `grok login` once. |
| **Cline** | `cline` | `cline --json --yolo --auto-approve true --cwd <dir> --timeout <sec> "<prompt>"` | [Cline CLI](https://docs.cline.bot/getting-started/authorizing-with-cline): `cline auth` (or `cline a`) and choose **Sign in with Cline** / ClinePass. Default provider is `cline` (subscription). | No public status command — run `cline auth` once; `cline config` inspects the saved session. |

Probes first run `--version`. When the CLI exposes a login-status command, a failed status is reported as **not logged in** (separate from **not installed**). WASM/Android skip process spawn and return **not supported**. No fake package versions are invented.

## Prerequisites

1. **.NET 10 SDK** (this repo targets `net10.0`; developed against SDK `10.0.401`).
2. **Desktop hosts only:** at least one coding-worker CLI on `PATH`, then its **subscription login**, if you want real file edits.
3. **Optional orchestration LLM** (`OpenAI:ApiKey` / `OPENAI_API_KEY`) for Jev's Agent Framework chat client. Unrelated to worker auth.
4. **Pack-only SDKs** (not required for `dotnet build` on Linux):
   - Windows pack: Windows 10/11 + `dotnet workload install maui-windows` + Windows App SDK
   - Android pack: `dotnet workload install maui-android` + Android SDK (`ANDROID_SDK_ROOT` or `ANDROID_HOME`)
   - Linux Photino runtime: WebKitGTK (e.g. `libwebkit2gtk-4.1-0` / GTK 4 on Ubuntu)

## Configure and switch

Copy [`.env.example`](.env.example) or use user secrets on `Jev.Web`:

```bash
cd src/Jev.Web
dotnet user-secrets set "OpenAI:ApiKey" "<orchestration-key>"
dotnet user-secrets set "Jev:CodingStrategy" "Claude"
```

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
| `CODEX_EXECUTABLE` / `CLAUDE_EXECUTABLE` / `GROK_EXECUTABLE` / `CLINE_EXECUTABLE` | Optional binary overrides |

## Run each host

```bash
dotnet restore
dotnet build          # Linux-safe solution (no MAUI TFMs)
dotnet test

dotnet run --project src/Jev.Web          # http://localhost:5296
dotnet run --project src/Jev.Wasm         # WASM dev server
dotnet run --project src/Jev.Linux        # Photino desktop (Linux/macOS/Windows)
```

MAUI (Windows or Android SDK required):

```bash
dotnet workload install maui-windows   # on Windows
dotnet workload install maui-android
dotnet build src/Jev.Maui/Jev.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build src/Jev.Maui/Jev.Maui.csproj -f net10.0-android
```

## NUKE packs

NUKE 10.1.0 lives in [`nuke/`](nuke/). From the repo root use `./build.sh` or, on Windows, `.\build.ps1` with the same targets:

```bash
./build.sh Compile
./build.sh Test
./build.sh PublishWasm
./build.sh PublishLinux              # tarball + .deb installer
./build.sh PublishWindowsPortable    # Photino win-x64 zip (cross-published from Linux)
./build.sh PackTool                  # Jev.Tool nupkg with linux-x64 + win-x64 payloads
./build.sh PublishWindows            # MAUI Windows; fails clearly unless Windows + maui-windows
./build.sh PublishAndroid            # fails clearly unless maui-android + Android SDK
./build.sh Pack                      # Test + WASM + Linux installers + Windows portable + tool
./build.sh PackAll                   # also MAUI Windows/Android; missing SDKs fail, they do not no-op
./build.sh Release                   # Pack + SHA256SUMS + GitHub prerelease v0.1.0
```

Install the desktop app as a **.NET tool** (recommended):

```bash
./build.sh PackTool
dotnet tool install -g Jev.Tool --add-source ./artifacts --version 0.1.0
jev                 # starts payload/linux-x64/Jev or payload/win-x64/Jev.exe
jev --tool-info     # prints detected RID and binary path
```

Outputs under `artifacts/`:

| File | Contents |
| --- | --- |
| `jev-wasm.zip` | WASM static site (`artifacts/wasm/wwwroot`) |
| `jev-linux-x64.tar.gz` | Self-contained Photino Linux app (`./Jev`) |
| `jev_<version>_amd64.deb` | Linux installer (`sudo dpkg -i …` then `jev`) |
| `jev-windows-x64.zip` | Photino + WebView2 portable Windows app (`Jev.exe`), cross-published from Linux |
| `Jev.Tool.<version>.nupkg` | `dotnet tool` with both desktop binaries and a host-detecting `jev` launcher |
| `jev-windows-maui-x64.zip` | MAUI unpackaged Windows app (Windows pack machine only). MSIX: `-p:WindowsPackageType=MSIX` |
| `SHA256SUMS` | SHA-256 checksums of the files above |
| `*.apk` | MAUI Android package (also copy any `.aab` the SDK emits) |

Install the Linux package with `sudo dpkg -i jev_0.1.0_amd64.deb`. The `.deb` depends on WebKitGTK (`libwebkit2gtk-4.1-0` or `libwebkit2gtk-4.0-37`).

## Try a sample chat

1. **Who are you?** — Jev answers from the persona layer. No worker process.
2. **Is the coding worker available?** — Agent Framework calls `probe_coding_worker`.
3. **Scaffold a hello console app in a temp workspace** — `scaffold_hello_console` on desktop hosts; WASM/Android report the host limitation.

## Architecture notes

### Microsoft Agent Framework

- `Microsoft.Agents.AI` 1.22.0
- `Microsoft.Agents.AI.OpenAI` 1.22.0

Jev is a `ChatClientAgent` created with `IChatClient.AsAIAgent(...)`, `AgentSession`, and `AIFunctionFactory` tools. Tools close over `ICodingStrategySelector.Active`.

### Host capabilities

`ICodingHost` is registered by each host:

| Host | `Name` | `SupportsLocalCli` |
| --- | --- | --- |
| `Jev.Web` | `server` | yes |
| `Jev.Linux` | `linux` | yes |
| `Jev.Maui` Windows | `windows` | yes |
| `Jev.Wasm` | `wasm` | no |
| `Jev.Maui` Android | `android` | no |

### Fallback orchestration

When the optional orchestration key is empty, `FallbackJevChatClient` still emits Agent Framework function calls. Worker auth stays subscription login on desktop CLIs.

## License

Use as a starting skeleton. Each worker CLI remains subject to its vendor terms.
