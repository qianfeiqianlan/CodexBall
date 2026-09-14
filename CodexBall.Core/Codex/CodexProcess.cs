using System.Diagnostics;

namespace CodexBall.Core.Codex;

public sealed class CodexProcess : IAsyncDisposable
{
    private Process? _process;

    public event EventHandler? Exited;

    public StreamWriter? StandardInput => _process?.StandardInput;

    public StreamReader? StandardOutput => _process?.StandardOutput;

    public StreamReader? StandardError => _process?.StandardError;

    public bool IsRunning => _process is { HasExited: false };

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return true;
        }

        await StopAsync(cancellationToken);

        var executable = await CodexLocator.LocateAsync(cancellationToken);
        if (executable is null)
        {
            return false;
        }

        var startInfo = executable.RequiresCommandShell
            ? CreateCmdStartInfo(executable.Path)
            : CreateDirectStartInfo(executable.Path);

        _process = Process.Start(startInfo);
        if (_process is null)
        {
            return false;
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken);
            }
        }
        catch
        {
            // Shutdown must stay best-effort.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private static ProcessStartInfo CreateDirectStartInfo(string path)
        => new()
        {
            FileName = path,
            Arguments = "app-server --stdio",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

    private static ProcessStartInfo CreateCmdStartInfo(string path)
        => new()
        {
            FileName = "cmd.exe",
            Arguments = $"/d /s /c \"\"{path}\" app-server --stdio\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
}
