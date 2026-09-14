using System.Collections.Concurrent;
using System.Text.Json;

namespace CodexBall.Core.Codex;

public sealed class CodexRpcClient : IAsyncDisposable
{
    private readonly CodexProcess _process;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private long _nextId;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;

    public CodexRpcClient(CodexProcess process)
    {
        _process = process;
    }

    public event EventHandler<JsonElement>? NotificationReceived;

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!await _process.StartAsync(cancellationToken))
        {
            return false;
        }

        _readerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token), CancellationToken.None);
        return true;
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (!_process.IsRunning || _process.StandardInput is null)
        {
            throw new InvalidOperationException("Codex app-server is not running.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        var request = parameters is null
            ? new Dictionary<string, object?> { ["id"] = id, ["method"] = method }
            : new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));
        await using var registration = timeoutCts.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.TrySetException(new TimeoutException($"RPC request timed out: {method}"));
            }
        });

        return await completion.Task;
    }

    public async Task NotifyAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        if (!_process.IsRunning || _process.StandardInput is null)
        {
            throw new InvalidOperationException("Codex app-server is not running.");
        }

        var request = parameters is null
            ? new Dictionary<string, object?> { ["method"] = method }
            : new Dictionary<string, object?> { ["method"] = method, ["params"] = parameters };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _process.StandardOutput;
        if (reader is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement.Clone();
                if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var id))
                {
                    if (_pending.TryRemove(id, out var completion))
                    {
                        if (root.TryGetProperty("error", out var error))
                        {
                            completion.TrySetException(new InvalidOperationException(error.ToString()));
                        }
                        else
                        {
                            completion.TrySetResult(root);
                        }
                    }
                }
                else if (root.TryGetProperty("method", out _))
                {
                    NotificationReceived?.Invoke(this, root);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_readerCts is not null)
        {
            await _readerCts.CancelAsync();
            _readerCts.Dispose();
        }

        await _process.DisposeAsync();
    }
}
