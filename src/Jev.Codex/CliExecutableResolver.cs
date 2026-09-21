using System.Collections.Concurrent;
using System.Diagnostics;

namespace Jev.Codex;

/// <summary>
/// Resolves coding-worker CLIs the way a user terminal does: PATH, Windows PATHEXT
/// (<c>.exe</c>/<c>.cmd</c>/<c>.bat</c>/<c>.ps1</c>), and common npm global bins.
/// Used by <c>CliProcessRunner</c> and <c>CodexProcessRunner</c>.
/// </summary>
public static class CliExecutableResolver
{
    private static readonly string[] PreferredWindowsExtensions = [".exe", ".cmd", ".bat", ".ps1"];
    private static readonly ConcurrentDictionary<string, string?> NpmBinCache = new(StringComparer.Ordinal);

    public static ResolvedCliExecutable? TryResolve(string? commandOrPath, CliSearchEnvironment? environment = null)
    {
        if (string.IsNullOrWhiteSpace(commandOrPath))
        {
            return null;
        }

        var env = environment ?? CliSearchEnvironment.Current;
        var trimmed = commandOrPath.Trim();

        if (HasDirectorySeparator(trimmed) || Path.IsPathRooted(trimmed))
        {
            return TryResolveExplicitPath(trimmed, trimmed, env);
        }

        foreach (var directory in EnumerateSearchDirectories(env))
        {
            var resolved = TryResolveInDirectory(directory, trimmed, env);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    public static bool TryApply(ProcessStartInfo startInfo, CliSearchEnvironment? environment = null)
    {
        var resolved = TryResolve(startInfo.FileName, environment);
        if (resolved is null)
        {
            return false;
        }

        ApplyTo(startInfo, resolved);
        return true;
    }

    public static void ApplyTo(ProcessStartInfo startInfo, ResolvedCliExecutable resolved)
    {
        var existing = startInfo.ArgumentList.ToArray();
        startInfo.FileName = resolved.FileName;
        startInfo.ArgumentList.Clear();

        switch (resolved.Kind)
        {
            case CliLaunchKind.WindowsCmdScript:
                startInfo.ArgumentList.Add("/d");
                startInfo.ArgumentList.Add("/s");
                startInfo.ArgumentList.Add("/c");
                var command = string.Join(' ', new[] { resolved.Path }.Concat(existing).Select(QuoteWindows));
                startInfo.ArgumentList.Add(command);
                break;
            case CliLaunchKind.WindowsPowerShellScript:
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(resolved.Path);
                foreach (var argument in existing)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                break;
            default:
                foreach (var argument in existing)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                break;
        }
    }

    public static string QuoteWindows(string value)
    {
        if (value.Length > 0 && value.AsSpan().IndexOfAny(" \t\"") < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static ResolvedCliExecutable? TryResolveExplicitPath(string command, string path, CliSearchEnvironment env)
    {
        var full = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        var existing = FindFile(full, env);
        if (existing is not null)
        {
            return CreateLaunch(command, PreferWindowsSiblingShim(existing, env), env);
        }

        if (env.IsWindows && string.IsNullOrEmpty(Path.GetExtension(full)))
        {
            foreach (var extension in GetWindowsExtensions(env))
            {
                var candidate = FindFile(full + extension, env);
                if (candidate is not null)
                {
                    return CreateLaunch(command, candidate, env);
                }
            }
        }

        return null;
    }

    private static ResolvedCliExecutable? TryResolveInDirectory(string directory, string command, CliSearchEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var hasExtension = !string.IsNullOrEmpty(Path.GetExtension(command));
        if (hasExtension)
        {
            var exact = FindFile(Path.Combine(directory, command), env);
            return exact is null ? null : CreateLaunch(command, exact, env);
        }

        if (env.IsWindows)
        {
            foreach (var extension in GetWindowsExtensions(env))
            {
                var candidate = FindFile(Path.Combine(directory, command + extension), env);
                if (candidate is not null)
                {
                    return CreateLaunch(command, candidate, env);
                }
            }
        }

        var bare = FindFile(Path.Combine(directory, command), env);
        return bare is null ? null : CreateLaunch(command, PreferWindowsSiblingShim(bare, env), env);
    }

    private static string PreferWindowsSiblingShim(string path, CliSearchEnvironment env)
    {
        if (!env.IsWindows || !string.IsNullOrEmpty(Path.GetExtension(path)))
        {
            return path;
        }

        foreach (var extension in PreferredWindowsExtensions)
        {
            var sibling = FindFile(path + extension, env);
            if (sibling is not null)
            {
                return sibling;
            }
        }

        return path;
    }

    private static ResolvedCliExecutable CreateLaunch(string command, string path, CliSearchEnvironment env)
    {
        var full = Path.GetFullPath(path);
        var extension = Path.GetExtension(full);
        if (env.IsWindows
            && (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            return new ResolvedCliExecutable(command, full, ResolveWindowsInterpreter("cmd.exe", env), CliLaunchKind.WindowsCmdScript);
        }

        if (env.IsWindows && extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            var powershell = File.Exists(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
                ? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")
                : ResolveWindowsInterpreter("powershell.exe", env);
            return new ResolvedCliExecutable(command, full, powershell, CliLaunchKind.WindowsPowerShellScript);
        }

        return new ResolvedCliExecutable(command, full, full, CliLaunchKind.Native);
    }

    private static string ResolveWindowsInterpreter(string name, CliSearchEnvironment env)
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(system))
        {
            var candidate = FindFile(Path.Combine(system, name), env);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return TryResolveInDirectory(Environment.GetFolderPath(Environment.SpecialFolder.System), name, env with { IsWindows = true })?.Path
               ?? name;
    }

    private static IEnumerable<string> EnumerateSearchDirectories(CliSearchEnvironment env)
    {
        var seen = new HashSet<string>(OperatingSystem.IsWindows() || env.IsWindows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        foreach (var directory in SplitPath(env.Path)
                     .Concat(env.ExtraDirectories)
                     .Concat(env.IncludeWellKnownDirectories ? WellKnownDirectories(env.IsWindows) : [])
                     .Concat(env.QueryNpmPrefix ? NpmGlobalBinDirectories(env) : []))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string full;
            try
            {
                full = Path.GetFullPath(directory);
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
            {
                continue;
            }

            if (seen.Add(full))
            {
                yield return full;
            }
        }
    }

    private static IEnumerable<string> WellKnownDirectories(bool isWindows)
    {
        if (isWindows)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                yield return Path.Combine(appData, "npm");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Combine(localAppData, "npm");
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                yield return Path.Combine(programFiles, "nodejs");
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, "nodejs");
            }

            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(user))
            {
                yield return Path.Combine(user, ".volta", "bin");
            }

            foreach (var name in new[] { "NVM_SYMLINK", "NVM_HOME" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            yield return Path.Combine(home, ".npm-global", "bin");
            yield return Path.Combine(home, ".local", "bin");
            yield return Path.Combine(home, ".volta", "bin");
            yield return Path.Combine(home, ".fnm", "current", "bin");
            yield return Path.Combine(home, ".nvm", "current", "bin");
        }

        yield return "/usr/local/bin";
    }

    private static IEnumerable<string> NpmGlobalBinDirectories(CliSearchEnvironment env)
    {
        var bin = TryGetNpmGlobalBin(env);
        if (!string.IsNullOrEmpty(bin))
        {
            yield return bin;
        }
    }

    private static string? TryGetNpmGlobalBin(CliSearchEnvironment env)
    {
        return NpmBinCache.GetOrAdd("npm-bin-g", _ => QueryNpmGlobalBin(env));
    }

    private static string? QueryNpmGlobalBin(CliSearchEnvironment env)
    {
        var npm = TryResolve("npm", env with { QueryNpmPrefix = false });
        if (npm is null)
        {
            return null;
        }

        return TryReadNpmDirectory(npm, env, ["bin", "-g"])
               ?? AppendUnixBin(TryReadNpmDirectory(npm, env, ["prefix", "-g"]), env.IsWindows);
    }

    private static string? TryReadNpmDirectory(ResolvedCliExecutable npm, CliSearchEnvironment env, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = npm.Command,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            ApplyTo(startInfo, npm);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var firstLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return !string.IsNullOrWhiteSpace(firstLine) && Directory.Exists(firstLine) ? firstLine : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static string? AppendUnixBin(string? prefix, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        if (isWindows)
        {
            return Directory.Exists(prefix) ? prefix : null;
        }

        var bin = Path.Combine(prefix, "bin");
        return Directory.Exists(bin) ? bin : Directory.Exists(prefix) ? prefix : null;
    }

    private static IReadOnlyList<string> GetWindowsExtensions(CliSearchEnvironment env)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return;
            }

            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }

            if (seen.Add(extension))
            {
                list.Add(extension);
            }
        }

        if (!string.IsNullOrWhiteSpace(env.PathExt))
        {
            foreach (var extension in env.PathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Add(extension);
            }
        }

        foreach (var extension in PreferredWindowsExtensions)
        {
            Add(extension);
        }

        return list;
    }

    /// <summary>
    /// Windows file systems are case-insensitive. Tests inject <see cref="CliSearchEnvironment.IsWindows"/>
    /// on Linux CI, so lookup must not depend on PATHEXT casing matching the on-disk name.
    /// </summary>
    private static string? FindFile(string path, CliSearchEnvironment env)
    {
        if (File.Exists(path))
        {
            return path;
        }

        if (!env.IsWindows)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name) || !Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitPath(string path)
        => path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool HasDirectorySeparator(string value)
        => value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar);
}
