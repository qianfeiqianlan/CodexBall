using System.Diagnostics;

namespace CodexBall.Core.Codex;

public sealed record CodexExecutable(string Path, bool RequiresCommandShell);

public static class CodexLocator
{
    public static async Task<CodexExecutable?> LocateAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "where.exe",
            Arguments = "codex",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return null;
        }

        var paths = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(File.Exists)
            .ToList();

        var path = paths.FirstOrDefault(IsWindowsLaunchable)
            ?? paths.FirstOrDefault();

        if (path is null)
        {
            return null;
        }

        var extension = System.IO.Path.GetExtension(path);
        return new CodexExecutable(path, extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExtension(string path, string extension)
        => System.IO.Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsLaunchable(string path)
        => HasExtension(path, ".cmd") || HasExtension(path, ".bat") || HasExtension(path, ".exe");
}
