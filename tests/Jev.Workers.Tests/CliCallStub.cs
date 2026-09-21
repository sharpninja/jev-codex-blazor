using System.Text;
using System.Text.Json;
using Jev.Codex;

namespace Jev.Workers.Tests;

/// <summary>
/// Writes a Codex / Claude / Grok / Cline stub the real process runners can launch.
/// The stub checks that strategy's documented argv contract, accepts UTF-8 stdin
/// when the CLI reads a prompt from stdin, and prints a recognizable success marker.
/// </summary>
internal sealed class CliCallStub : IDisposable
{
    public CliCallStub(CodingStrategyKind kind)
    {
        Kind = kind;
        Path = Directory.CreateTempSubdirectory("jev-cli-call-").FullName;
        CaptureDirectory = System.IO.Path.Combine(Path, "capture");
        Directory.CreateDirectory(CaptureDirectory);
        ExecutablePath = WriteStub();
    }

    public CodingStrategyKind Kind { get; }

    public string Path { get; }

    public string CaptureDirectory { get; }

    public string ExecutablePath { get; }

    public string CommandName => Kind switch
    {
        CodingStrategyKind.Codex => "codex",
        CodingStrategyKind.Claude => "claude",
        CodingStrategyKind.GrokBuild => "grok",
        CodingStrategyKind.Cline => "cline",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };

    public string SuccessMarker => Kind switch
    {
        CodingStrategyKind.Codex => "CODEX_STUB_OK",
        CodingStrategyKind.Claude => "CLAUDE_STUB_OK",
        CodingStrategyKind.GrokBuild => "GROK_STUB_OK",
        CodingStrategyKind.Cline => "CLINE_STUB_OK",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };

    public string SessionId => Kind switch
    {
        CodingStrategyKind.Codex => "codex-stub-thread",
        CodingStrategyKind.Claude => "claude-stub-session",
        CodingStrategyKind.GrokBuild => "grok-stub-session",
        CodingStrategyKind.Cline => "cline-stub-session",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };

    public CliSearchEnvironment IsolatedEnvironment => new()
    {
        Path = Path,
        PathExt = OperatingSystem.IsWindows() ? ".COM;.EXE;.BAT;.CMD;.PS1" : null,
        IsWindows = OperatingSystem.IsWindows(),
        IncludeWellKnownDirectories = false,
        QueryNpmPrefix = false
    };

    public IReadOnlyList<string>? TryReadArgv()
    {
        var json = System.IO.Path.Combine(CaptureDirectory, "argv.json");
        if (!File.Exists(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(File.ReadAllText(json));
    }

    public byte[] ReadStdin()
    {
        var file = System.IO.Path.Combine(CaptureDirectory, "stdin.bin");
        return File.Exists(file) ? File.ReadAllBytes(file) : [];
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string WriteStub()
    {
        if (OperatingSystem.IsWindows())
        {
            var file = System.IO.Path.Combine(Path, CommandName + ".ps1");
            File.WriteAllText(file, BuildPowerShell(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return file;
        }

        var unix = System.IO.Path.Combine(Path, CommandName);
        File.WriteAllText(unix, BuildUnixShell(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(
            unix,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return unix;
    }

    private string BuildUnixShell()
        => $$"""
            #!/bin/sh
            set -e
            CAPTURE='{{CaptureDirectory.Replace("'", "'\\''")}}'
            KIND='{{CommandName}}'

            has() {
              needle=$1
              shift
              for a in "$@"; do
                [ "$a" = "$needle" ] && return 0
              done
              return 1
            }

            fail() {
              echo "$1" >&2
              exit 2
            }

            dump() {
              mkdir -p "$CAPTURE"
              if command -v python3 >/dev/null 2>&1; then
                python3 -c 'import json,sys; json.dump(sys.argv[2:], open(sys.argv[1],"w"), ensure_ascii=False)' "$CAPTURE/argv.json" "$@"
              fi
              cat > "$CAPTURE/stdin.bin" || true
            }

            if [ "$KIND" = "cline" ]; then
              if [ "${1:-}" = "version" ] || [ "${1:-}" = "--version" ] || [ "${1:-}" = "-V" ]; then
                echo "cline-stub 0.0"
                exit 0
              fi
            elif [ "${1:-}" = "--version" ]; then
              echo "${KIND}-stub 0.0"
              exit 0
            fi

            if [ "$KIND" = "codex" ] && [ "${1:-}" = "login" ] && [ "${2:-}" = "status" ]; then
              exit 0
            fi
            if [ "$KIND" = "claude" ] && [ "${1:-}" = "auth" ] && [ "${2:-}" = "status" ]; then
              exit 0
            fi

            dump "$@"

            case "$KIND" in
              codex)
                has --ask-for-approval "$@" || fail "missing --ask-for-approval"
                has never "$@" || fail "missing approval never"
                has exec "$@" || fail "missing exec"
                has --json "$@" || fail "missing --json"
                has --sandbox "$@" || fail "missing --sandbox"
                has --skip-git-repo-check "$@" || fail "missing --skip-git-repo-check"
                has --cd "$@" || fail "missing --cd"
                has - "$@" || fail "missing stdin -"
                approval=-1
                execpos=-1
                i=0
                for a in "$@"; do
                  i=$((i+1))
                  [ "$a" = "--ask-for-approval" ] && approval=$i
                  [ "$a" = "exec" ] && execpos=$i
                done
                [ "$approval" -ge 0 ] && [ "$execpos" -gt "$approval" ] || fail "approval must precede exec"
                [ -s "$CAPTURE/stdin.bin" ] || fail "empty stdin"
                if command -v python3 >/dev/null 2>&1; then
                  python3 -c 'from pathlib import Path; import sys; Path(sys.argv[1]).read_bytes().decode("utf-8")' "$CAPTURE/stdin.bin" \
                    || fail "invalid UTF-8 stdin"
                fi
                printf '%s\n' '{"type":"thread.started","thread_id":"{{SessionId}}"}'
                printf '%s\n' '{"type":"item.completed","item":{"type":"agent_message","text":"{{SuccessMarker}}"}}'
                ;;
              claude)
                has -p "$@" || fail "missing -p"
                has --output-format "$@" || fail "missing --output-format"
                has json "$@" || fail "missing json"
                has --permission-mode "$@" || fail "missing --permission-mode"
                has acceptEdits "$@" || fail "missing acceptEdits"
                has --allowedTools "$@" || fail "missing --allowedTools"
                printf '%s\n' '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
                ;;
              grok)
                has -p "$@" || fail "missing -p"
                has --output-format "$@" || fail "missing --output-format"
                has streaming-json "$@" || fail "missing streaming-json"
                has --cwd "$@" || fail "missing --cwd"
                has --always-approve "$@" || fail "missing --always-approve"
                printf '%s\n' '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
                ;;
              cline)
                has --json "$@" || fail "missing --json"
                has --yolo "$@" || fail "missing --yolo"
                has --auto-approve "$@" || fail "missing --auto-approve"
                has --cwd "$@" || fail "missing --cwd"
                has --timeout "$@" || fail "missing --timeout"
                printf '%s\n' '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
                ;;
              *)
                fail "unknown stub kind $KIND"
                ;;
            esac
            """;

    private string BuildPowerShell()
        => $$"""
            $ErrorActionPreference = 'Stop'
            $capture = '{{CaptureDirectory.Replace("'", "''")}}'
            $kind = '{{CommandName}}'
            $argv = @($args)

            function Fail([string]$message) {
              [Console]::Error.WriteLine($message)
              exit 2
            }

            function Has([string]$flag) {
              return $argv -contains $flag
            }

            function Dump {
              New-Item -ItemType Directory -Force -Path $capture | Out-Null
              ConvertTo-Json -InputObject @($argv) -Compress | Set-Content -LiteralPath (Join-Path $capture 'argv.json') -Encoding utf8
              $fs = [System.IO.File]::Create((Join-Path $capture 'stdin.bin'))
              try {
                [Console]::OpenStandardInput().CopyTo($fs)
              } finally {
                $fs.Dispose()
              }
            }

            if ($kind -eq 'cline' -and $argv.Count -ge 1 -and @('version','--version','-V') -contains $argv[0]) {
              Write-Output 'cline-stub 0.0'
              exit 0
            }
            if ($kind -ne 'cline' -and $argv.Count -ge 1 -and $argv[0] -eq '--version') {
              Write-Output ($kind + '-stub 0.0')
              exit 0
            }
            if ($kind -eq 'codex' -and $argv.Count -ge 2 -and $argv[0] -eq 'login' -and $argv[1] -eq 'status') {
              exit 0
            }
            if ($kind -eq 'claude' -and $argv.Count -ge 2 -and $argv[0] -eq 'auth' -and $argv[1] -eq 'status') {
              exit 0
            }

            Dump

            switch ($kind) {
              'codex' {
                if (-not (Has '--ask-for-approval')) { Fail 'missing --ask-for-approval' }
                if (-not (Has 'never')) { Fail 'missing approval never' }
                if (-not (Has 'exec')) { Fail 'missing exec' }
                if (-not (Has '--json')) { Fail 'missing --json' }
                if (-not (Has '--sandbox')) { Fail 'missing --sandbox' }
                if (-not (Has '--skip-git-repo-check')) { Fail 'missing --skip-git-repo-check' }
                if (-not (Has '--cd')) { Fail 'missing --cd' }
                if (-not (Has '-')) { Fail 'missing stdin -' }
                $approval = [Array]::IndexOf($argv, '--ask-for-approval')
                $exec = [Array]::IndexOf($argv, 'exec')
                if ($approval -lt 0 -or $exec -lt 0 -or $approval -ge $exec) { Fail 'approval must precede exec' }
                $stdinPath = Join-Path $capture 'stdin.bin'
                $bytes = [System.IO.File]::ReadAllBytes($stdinPath)
                if ($bytes.Length -eq 0) { Fail 'empty stdin' }
                $utf8 = New-Object System.Text.UTF8Encoding $false, $true
                try { $null = $utf8.GetString($bytes) } catch { Fail 'invalid UTF-8 stdin' }
                Write-Output '{"type":"thread.started","thread_id":"{{SessionId}}"}'
                Write-Output '{"type":"item.completed","item":{"type":"agent_message","text":"{{SuccessMarker}}"}}'
              }
              'claude' {
                if (-not (Has '-p')) { Fail 'missing -p' }
                if (-not (Has '--output-format')) { Fail 'missing --output-format' }
                if (-not (Has 'json')) { Fail 'missing json' }
                if (-not (Has '--permission-mode')) { Fail 'missing --permission-mode' }
                if (-not (Has 'acceptEdits')) { Fail 'missing acceptEdits' }
                if (-not (Has '--allowedTools')) { Fail 'missing --allowedTools' }
                Write-Output '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
              }
              'grok' {
                if (-not (Has '-p')) { Fail 'missing -p' }
                if (-not (Has '--output-format')) { Fail 'missing --output-format' }
                if (-not (Has 'streaming-json')) { Fail 'missing streaming-json' }
                if (-not (Has '--cwd')) { Fail 'missing --cwd' }
                if (-not (Has '--always-approve')) { Fail 'missing --always-approve' }
                Write-Output '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
              }
              'cline' {
                if (-not (Has '--json')) { Fail 'missing --json' }
                if (-not (Has '--yolo')) { Fail 'missing --yolo' }
                if (-not (Has '--auto-approve')) { Fail 'missing --auto-approve' }
                if (-not (Has '--cwd')) { Fail 'missing --cwd' }
                if (-not (Has '--timeout')) { Fail 'missing --timeout' }
                Write-Output '{"result":"{{SuccessMarker}}","session_id":"{{SessionId}}"}'
              }
              default { Fail "unknown stub kind $kind" }
            }
            """;
}
