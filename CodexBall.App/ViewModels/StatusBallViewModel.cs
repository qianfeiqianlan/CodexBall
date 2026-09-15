using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using CodexBall.App.Services;
using CodexBall.Core.Codex;
using CodexBall.Core.Models;
using CodexBall.Core.Utils;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace CodexBall.App.ViewModels;

public sealed class StatusBallViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly CodexUsageService _usageService = new();
    private readonly CodexProcessMonitor _processMonitor;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _debounceTimer;
    private CodexUsageSnapshot _snapshot = CodexUsageSnapshot.Unavailable(AccountState.Unknown, "Loading");
    private bool _isRefreshing;
    private bool _isCodexActive;

    public StatusBallViewModel(CodexProcessMonitor processMonitor)
    {
        _processMonitor = processMonitor;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _clockTimer.Tick += (_, _) => NotifyComputedProperties();

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await RefreshAsync();
        };

        _usageService.RateLimitsUpdated += (_, _) =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<bool>? CodexActivityChanged;

    public bool IsCodexActive => _isCodexActive;

    public int? TrayUsagePercent => _isCodexActive ? _snapshot.ShortWindow?.RemainingPercent : null;

    public string CenterText => _snapshot.ShortWindow is null ? "--" : _snapshot.ShortWindow.RemainingPercent.ToString();

    public double Progress => _snapshot.ShortWindow?.RemainingPercent ?? 0;

    public string StatusText => _snapshot.AccountState switch
    {
        AccountState.Available => "Codex",
        AccountState.NotLoggedIn => "Not logged in",
        AccountState.CodexNotFound => "Codex not found",
        _ => "Unavailable"
    };

    public Brush RingBrush => _snapshot.ShortWindow?.RemainingPercent switch
    {
        null => new SolidColorBrush(Color.FromRgb(130, 140, 150)),
        >= 50 => new SolidColorBrush(Color.FromRgb(57, 217, 138)),
        >= 20 => new SolidColorBrush(Color.FromRgb(245, 187, 64)),
        _ => new SolidColorBrush(Color.FromRgb(246, 91, 91))
    };

    public string TooltipText => RateLimitFormatter.FormatTooltip(_snapshot);

    public string ShortWindowLabel => _snapshot.ShortWindow is null
        ? "Short window"
        : RateLimitFormatter.FormatWindowLabel(_snapshot.ShortWindow.WindowDurationMins);

    public string ShortWindowPercent => _snapshot.ShortWindow is null ? "--" : $"{_snapshot.ShortWindow.RemainingPercent}% left";

    public double ShortWindowProgress => _snapshot.ShortWindow?.RemainingPercent ?? 0;

    public string ShortWindowReset => _snapshot.ShortWindow is null ? _snapshot.Message ?? "Unavailable" : $"Reset {TimeFormatter.FormatReset(_snapshot.ShortWindow.ResetsAt)}";

    public string LongWindowLabel => _snapshot.LongWindow is null
        ? "Long window"
        : RateLimitFormatter.FormatWindowLabel(_snapshot.LongWindow.WindowDurationMins);

    public string LongWindowPercent => _snapshot.LongWindow is null ? "--" : $"{_snapshot.LongWindow.RemainingPercent}% left";

    public double LongWindowProgress => _snapshot.LongWindow?.RemainingPercent ?? 0;

    public string LongWindowReset => _snapshot.LongWindow is null ? "Unavailable" : $"Reset {TimeFormatter.FormatReset(_snapshot.LongWindow.ResetsAt)}";

    public string LastUpdatedText => _snapshot.AccountState == AccountState.Available
        ? $"Last update {_snapshot.LastUpdatedAt.LocalDateTime:HH:mm}"
        : _snapshot.Message ?? "Unavailable";

    public async Task StartAsync()
    {
        _pollTimer.Start();
        _clockTimer.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            if (!_processMonitor.IsCodexRunning())
            {
                SetCodexActive(false);
                await _usageService.StopAsync();
                return;
            }

            SetCodexActive(true);
            Snapshot = await _usageService.RefreshAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private CodexUsageSnapshot Snapshot
    {
        set
        {
            _snapshot = value;
            NotifyComputedProperties();
        }
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(CenterText));
        OnPropertyChanged(nameof(TrayUsagePercent));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RingBrush));
        OnPropertyChanged(nameof(TooltipText));
        OnPropertyChanged(nameof(ShortWindowLabel));
        OnPropertyChanged(nameof(ShortWindowPercent));
        OnPropertyChanged(nameof(ShortWindowProgress));
        OnPropertyChanged(nameof(ShortWindowReset));
        OnPropertyChanged(nameof(LongWindowLabel));
        OnPropertyChanged(nameof(LongWindowPercent));
        OnPropertyChanged(nameof(LongWindowProgress));
        OnPropertyChanged(nameof(LongWindowReset));
        OnPropertyChanged(nameof(LastUpdatedText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetCodexActive(bool isActive)
    {
        if (_isCodexActive == isActive)
        {
            return;
        }

        _isCodexActive = isActive;
        OnPropertyChanged(nameof(IsCodexActive));
        OnPropertyChanged(nameof(TrayUsagePercent));
        CodexActivityChanged?.Invoke(this, isActive);
    }

    public async ValueTask DisposeAsync()
    {
        _pollTimer.Stop();
        _clockTimer.Stop();
        _debounceTimer.Stop();
        await _usageService.DisposeAsync();
    }
}
