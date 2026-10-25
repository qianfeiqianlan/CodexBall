using System.Runtime.InteropServices;

namespace CodexBall.Core.Codex;

public sealed record CodexExecutable(string Path, bool RequiresCommandShell);

public static class CodexLocator
{
    public static Task<CodexExecutable?> LocateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var windows = OperatingSystem.IsWindows();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (windows) directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
        else
        {
            directories.AddRange(["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", Path.Combine(home, ".local/bin"), Path.Combine(home, ".npm-global/bin"), "/Applications/Codex.app/Contents/Resources", Path.Combine(home, "Applications/Codex.app/Contents/Resources")]);
            // Finder/LaunchAgent environments often omit Node and Homebrew locations from PATH.
            var nvm = Path.Combine(home, ".nvm/versions/node");
            if (Directory.Exists(nvm))
            {
                try { directories.AddRange(Directory.GetDirectories(nvm).OrderByDescending(p => ParseNodeVersion(Path.GetFileName(p))).Select(p => Path.Combine(p, "bin"))); }
                catch (UnauthorizedAccessException) { }
            }
        }
        return Task.FromResult(FindInDirectories(directories, windows));
    }

    private static Version ParseNodeVersion(string text) => Version.TryParse(text.TrimStart('v'), out var version) ? version : new Version();

    public static CodexExecutable? FindInDirectories(IEnumerable<string> directories, bool windows)
    {
        foreach (var directory in directories.Distinct(windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            foreach (var name in windows ? new[] { "codex.exe", "codex.cmd", "codex.bat" } : new[] { "codex" })
            {
                var path = Path.Combine(directory.Trim('"'), name);
                if (!File.Exists(path)) continue;
                if (!windows && !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0) continue;
                return new(Path.GetFullPath(path), windows && Path.GetExtension(path) is ".cmd" or ".bat");
            }
        return null;
    }
}
