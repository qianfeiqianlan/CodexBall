using System.Text.Json;
using CodexBall.Core.Models;

namespace CodexBall.Core.Codex;

public sealed class CodexUsageService : IAsyncDisposable
{
    private readonly CodexProcess _process = new();
    private CodexRpcClient? _rpcClient;
    private bool _initialized;

    public event EventHandler? RateLimitsUpdated;

    public async Task<CodexUsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await EnsureInitializedAsync(cancellationToken))
            {
                return CodexUsageSnapshot.Unavailable(AccountState.CodexNotFound, "Codex not found");
            }

            var account = await _rpcClient!.SendAsync("account/read", new { refreshToken = false }, cancellationToken: cancellationToken);
            if (!HasAccount(account))
            {
                return CodexUsageSnapshot.Unavailable(AccountState.NotLoggedIn, "Codex is not logged in");
            }

            var rateLimits = await _rpcClient.SendAsync("account/rateLimits/read", cancellationToken: cancellationToken);
            var (shortWindow, longWindow) = RateLimitParser.Parse(rateLimits);
            if (shortWindow is null)
            {
                return CodexUsageSnapshot.Unavailable(AccountState.Error, "Rate limit unavailable");
            }

            return new CodexUsageSnapshot(AccountState.Available, shortWindow, longWindow, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _initialized = false;
            return CodexUsageSnapshot.Unavailable(AccountState.Error, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        _initialized = false;
        await DisposeRpcAsync();
    }

    private async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized && _rpcClient is not null)
        {
            return true;
        }

        await DisposeRpcAsync();

        _rpcClient = new CodexRpcClient(_process);
        _rpcClient.NotificationReceived += OnNotificationReceived;

        if (!await _rpcClient.StartAsync(cancellationToken))
        {
            return false;
        }

        await _rpcClient.SendAsync("initialize", new
        {
            clientInfo = new
            {
                name = "codex-ball",
                title = "Codex Ball",
                version = "0.1.0"
            },
            capabilities = new
            {
                experimentalApi = true
            }
        }, cancellationToken: cancellationToken);

        await _rpcClient.NotifyAsync("initialized", cancellationToken: cancellationToken);
        _initialized = true;
        return true;
    }

    private static bool HasAccount(JsonElement response)
    {
        var root = response.TryGetProperty("result", out var result) ? result : response;
        if (root.TryGetProperty("account", out var account))
        {
            return account.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
        }

        return root.ValueKind is JsonValueKind.Object && root.EnumerateObject().Any();
    }

    private void OnNotificationReceived(object? sender, JsonElement notification)
    {
        if (notification.TryGetProperty("method", out var method)
            && method.GetString() == "account/rateLimits/updated")
        {
            RateLimitsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task DisposeRpcAsync()
    {
        if (_rpcClient is null)
        {
            return;
        }

        _rpcClient.NotificationReceived -= OnNotificationReceived;
        await _rpcClient.DisposeAsync();
        _rpcClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
