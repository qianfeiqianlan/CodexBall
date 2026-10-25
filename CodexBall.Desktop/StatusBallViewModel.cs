using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using CodexBall.App.Services;
using CodexBall.Core.Codex;
using CodexBall.Core.Models;
using CodexBall.Core.Utils;
using CodexBall.Core.Companion;
using Brush = Avalonia.Media.IBrush;
using Color = Avalonia.Media.Color;

namespace CodexBall.Desktop;

public sealed class StatusBallViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly CodexUsageService _usageService = new();
    private readonly CodexProcessMonitor _processMonitor;
    private readonly DispatcherTimer _activityTimer;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _refreshTask;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _debounceTimer;
    private CodexUsageSnapshot _snapshot = CodexUsageSnapshot.Unavailable(AccountState.Unknown, "Loading");
    private bool _isRefreshing;
    private bool _isCodexActive;
    private readonly QuotaReactionTracker _quotaReactions = new();
    public event EventHandler<CompanionAction>? CompanionReaction;

    public StatusBallViewModel(CodexProcessMonitor processMonitor)
    {
        _processMonitor = processMonitor;
        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _activityTimer.Tick += async (_, _) => { if (_processMonitor.IsCodexRunning() != IsCodexActive) await RefreshAsync(); };
        AppLocalization.Current.PropertyChanged += OnLanguageChanged;
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
            Dispatcher.UIThread.Post(() => { if (_shutdown.IsCancellationRequested) return; _debounceTimer.Stop(); _debounceTimer.Start(); });
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<bool>? CodexActivityChanged;

    public bool IsCodexActive => _isCodexActive;

    public int? TrayUsagePercent => _isCodexActive ? _snapshot.ShortWindow?.RemainingPercent : null;

    public string CenterText => _snapshot.ShortWindow is null ? "--" : _snapshot.ShortWindow.RemainingPercent.ToString();

    public double Progress => _snapshot.ShortWindow?.RemainingPercent ?? 0;

    public double ResetProgress => CalculateResetProgress(_snapshot.ShortWindow);

    public string StatusText => _snapshot.AccountState switch
    {
        AccountState.Available => "Codex",
        AccountState.NotLoggedIn => AppLocalization.Text("Not logged in"),
        AccountState.CodexNotFound => AppLocalization.Text("Codex not found"),
        _ => AppLocalization.Text("Unavailable")
    };

    public Brush RingBrush => _snapshot.ShortWindow?.RemainingPercent switch
    {
        null => new SolidColorBrush(Color.FromRgb(130, 140, 150)),
        >= 50 => new SolidColorBrush(Color.FromRgb(57, 217, 138)),
        >= 20 => new SolidColorBrush(Color.FromRgb(245, 187, 64)),
        _ => new SolidColorBrush(Color.FromRgb(246, 91, 91))
    };

    public Brush ResetRingBrush => _snapshot.ShortWindow?.ResetsAt is null
        ? new SolidColorBrush(Color.FromRgb(130, 140, 150))
        : new SolidColorBrush(Color.FromRgb(75, 179, 255));

    public string TooltipText => BuildTooltip();

    public string ShortWindowLabel => _snapshot.ShortWindow is null
        ? AppLocalization.Text("Short window")
        : RateLimitFormatter.FormatWindowLabel(_snapshot.ShortWindow.WindowDurationMins, AppLocalization.Current.IsChinese);

    public string ShortWindowPercent => _snapshot.ShortWindow is null ? "--" : AppLocalization.Format("{0}% left", _snapshot.ShortWindow.RemainingPercent);

    public double ShortWindowProgress => _snapshot.ShortWindow?.RemainingPercent ?? 0;

    public string ShortWindowReset => _snapshot.ShortWindow is null ? AppLocalization.Text(_snapshot.Message ?? "Unavailable") : AppLocalization.Format("Reset {0}", TimeFormatter.FormatReset(_snapshot.ShortWindow.ResetsAt, chinese: AppLocalization.Current.IsChinese));

    public string LongWindowLabel => _snapshot.LongWindow is null
        ? AppLocalization.Text("Long window")
        : RateLimitFormatter.FormatWindowLabel(_snapshot.LongWindow.WindowDurationMins, AppLocalization.Current.IsChinese);

    public string LongWindowPercent => _snapshot.LongWindow is null ? "--" : AppLocalization.Format("{0}% left", _snapshot.LongWindow.RemainingPercent);

    public double LongWindowProgress => _snapshot.LongWindow?.RemainingPercent ?? 0;

    public string LongWindowReset => _snapshot.LongWindow is null ? AppLocalization.Text("Unavailable") : AppLocalization.Format("Reset {0}", TimeFormatter.FormatReset(_snapshot.LongWindow.ResetsAt, chinese: AppLocalization.Current.IsChinese));

    public string LastUpdatedText => _snapshot.AccountState == AccountState.Available
        ? AppLocalization.Format("Last update {0}", _snapshot.LastUpdatedAt.LocalDateTime.ToString("HH:mm"))
        : AppLocalization.Text(_snapshot.Message ?? "Unavailable");

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => NotifyComputedProperties();

    private string BuildTooltip()
    {
        if (_snapshot.ShortWindow is null) return $"Codex\n{AppLocalization.Text(_snapshot.Message ?? "Unavailable")}";
        var lines = new List<string> { "Codex", "", $"{ShortWindowLabel}  {ShortWindowPercent}" };
        if (_snapshot.LongWindow is not null && _snapshot.LongWindow != _snapshot.ShortWindow)
            lines.Add($"{LongWindowLabel}  {LongWindowPercent}");
        lines.Add(ShortWindowReset);
        return string.Join(Environment.NewLine, lines);
    }

    public async Task StartAsync()
    {
        _activityTimer.Start();
        _pollTimer.Start();
        _clockTimer.Start();
        await RefreshAsync();
    }

    public Task RefreshAsync() => _refreshTask is { IsCompleted: false } ? _refreshTask : _refreshTask = RefreshCoreAsync();
    private async Task RefreshCoreAsync()
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
                Snapshot = CodexUsageSnapshot.Unavailable(AccountState.Unknown, "Codex is not running");
                SetCodexActive(false);
                await _usageService.StopAsync();
                return;
            }

            SetCodexActive(true);
            Snapshot = await _usageService.RefreshAsync(_shutdown.Token);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void ApplySnapshot(CodexUsageSnapshot snapshot, bool active) { SetCodexActive(active); Snapshot = snapshot; }
    private CodexUsageSnapshot Snapshot
    {
        set
        {
            _snapshot = value;
            NotifyComputedProperties();
            if (_quotaReactions.Observe(value.ShortWindow) is { } reaction)
                CompanionReaction?.Invoke(this, reaction);
        }
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(CenterText));
        OnPropertyChanged(nameof(TrayUsagePercent));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ResetProgress));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RingBrush));
        OnPropertyChanged(nameof(ResetRingBrush));
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

    private static double CalculateResetProgress(RateLimitWindow? window)
    {
        if (window?.ResetsAt is null || window.WindowDurationMins <= 0)
        {
            return 0;
        }

        var remaining = window.ResetsAt.Value - DateTimeOffset.Now;
        var total = TimeSpan.FromMinutes(window.WindowDurationMins);
        return Math.Clamp(remaining.TotalMilliseconds / total.TotalMilliseconds * 100, 0, 100);
    }

    public async ValueTask DisposeAsync()
    {
        AppLocalization.Current.PropertyChanged -= OnLanguageChanged;
        _activityTimer.Stop();
        _shutdown.Cancel();
        if (_refreshTask is not null) await _refreshTask;
        _pollTimer.Stop();
        _clockTimer.Stop();
        _debounceTimer.Stop();
        await _usageService.DisposeAsync();
    }
}
