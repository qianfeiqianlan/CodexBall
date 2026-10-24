using CodexBall.App.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CodexBall.App.Companion;
using CodexBall.App.Companion.Live2D;
using CodexBall.Core.Companion;
using MessageBox = System.Windows.MessageBox;

namespace CodexBall.App;

public partial class MainWindow
{
    private ICompanionPlayer? _player;
    private LoadedSkin? _skin;
    private CompanionAction _action;
    private readonly DispatcherTimer _sleepTimer = new() { Interval = TimeSpan.FromMinutes(3) };
    private const double CompanionFooterHeight = 20;
    private bool _usageBarClick;
    private bool _headClick;
    private readonly SemaphoreSlim _appearanceCycleLock = new(1, 1);
    public bool IsCompanionMode => _settings.CompanionMode;
    public string SelectedSkinId => _settings.SkinId;

    private void InitializeCompanion()
    {
        _sleepTimer.Tick += (_, _) => { _sleepTimer.Stop(); PlayAction(CompanionAction.Sleep); };
        _viewModel.CompanionReaction += OnCompanionReaction;
        IsVisibleChanged += (_, _) => UpdatePlayback();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        LoadSelectedSkin();
        ApplyDisplayMode();
        MouseLeave += (_, _) => (_player as IInteractiveCompanionPlayer)?.SetLookTarget(0, 0);
        LocationChanged += (_, _) =>
        {
            if (_isDragging) (_player as IInteractiveCompanionPlayer)?.SetDragImpulse(Left - _dragPreviousLeft);
            _dragPreviousLeft = Left;
        };
    }

    private void LoadSelectedSkin()
    {
        var entry = SkinCatalog.Discover(out _).FirstOrDefault(e => e.Id == _settings.SkinId);
        if (!_settings.CompanionMode || entry?.Directory is null)
        {
            UseBallAppearance();
            return;
        }
        try { InstallSkin(SkinLoader.Load(entry.Directory)); }
        catch (Exception ex) when (IsSkinError(ex))
        {
            UseBallAppearance();
            Dispatcher.BeginInvoke(() => MessageBox.Show(AppLocalization.Format("Appearance failed to load. Restored CodexBall.\n{0}", ex.Message), "CodexBall"));
        }
    }

    private void UseBallAppearance()
    {
        _player?.Dispose();
        _player = null;
        _skin = null;
        CharacterImage.Source = null;
        _settings.SkinId = SkinCatalog.BallId;
        _settings.CompanionMode = false;
    }
    private static bool IsSkinError(Exception ex)
        => ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
            or ArgumentException or NotSupportedException or InvalidOperationException or OverflowException or FormatException;

    private void InstallSkin(LoadedSkin skin)
    {
        // Fully decode and validate before replacing the currently usable player.
        ICompanionPlayer player = skin.Definition.Live2DModel is null
            ? new PngCompanionPlayer(skin) : new Live2DCompanionPlayer(skin);
        _player?.Dispose();
        _skin = skin;
        _player = player;
        _action = CompanionAction.Idle;
        player.FrameChanged += (_, _) => UpdateCharacterFrame();
        player.Completed += (_, _) => PlayAction(CompanionAction.Idle, force: true);
        if (player is Live2DCompanionPlayer live2D)
            live2D.Failed += (_, error) => Dispatcher.BeginInvoke(async () =>
            {
                if (!ReferenceEquals(_player, live2D)) return;
                await SelectAppearanceAsync(SkinCatalog.BallId);
                MessageBox.Show(AppLocalization.Format("Live2D rendering stopped. Restored CodexBall.\n{0}", error.Message), "CodexBall");
            });
        UpdateCharacterFrame();
    }

    private void UpdateCharacterFrame()
    {
        if (_player is null || _skin is null) return;
        CharacterImage.Source = _player.Image;
        var bounds = _player.ImageBounds;
        CharacterImage.Width = bounds.Width;
        CharacterImage.Height = bounds.Height;
        Canvas.SetLeft(CharacterImage, bounds.X);
        Canvas.SetTop(CharacterImage, bounds.Y);
    }

    private void ApplyDisplayMode()
    {
        UsagePopup.IsOpen = false;
        var companion = IsCompanionMode;
        BallView.Visibility = companion ? Visibility.Collapsed : Visibility.Visible;
        CompanionView.Visibility = companion ? Visibility.Visible : Visibility.Collapsed;
        CharacterCanvas.Width = _skin?.Definition.Width ?? 64;
        CharacterCanvas.Height = _skin?.Definition.Height ?? 64;
        UsageBar.RenderTransform = new System.Windows.Media.TranslateTransform(0, _skin?.Definition.UsageBarOffsetY ?? 0);
        Width = Root.Width = companion ? Math.Max(160, _skin!.Definition.Width) : 64;
        Height = Root.Height = companion ? _skin!.Definition.Height + CompanionFooterHeight : 64;
        ClampToWorkArea();
        CaptureVisiblePosition();
        UpdatePlayback();
    }

    public async Task CycleAppearanceAsync()
    {
        await _appearanceCycleLock.WaitAsync();
        try
        {
            var entries = SkinCatalog.Discover(out _);
            if (entries.Count <= 1) return;
            var current = entries.ToList().FindIndex(e => e.Id == SelectedSkinId);
            await SelectAppearanceAsync(entries[(current + 1) % entries.Count].Id);
        }
        finally { _appearanceCycleLock.Release(); }
    }

    public async Task SelectAppearanceAsync(string id)
    {
        var entry = SkinCatalog.Discover(out _).FirstOrDefault(e => e.Id == id);
        if (entry is null) return;
        try
        {
            if (entry.Directory is null)
            {
                ShowVisible(animate: false);
                UseBallAppearance();
                ShowResetRing = _settings.ShowResetRing = true;
            }
            else
            {
                InstallSkin(SkinLoader.Load(entry.Directory));
                ShowVisible(animate: false);
                _settings.SkinId = entry.Id;
                _settings.CompanionMode = true;
            }
            ApplyDisplayMode();
            if (IsCompanionMode) ShowForCodex();
            else if (!_viewModel.IsCodexActive) HideForCodex();
            if (_settings.EdgeHideEnabled) EnterEdgeHideMode(animate: false);
            await SavePositionAsync();
        }
        catch (Exception ex) when (IsSkinError(ex))
        {
            MessageBox.Show(AppLocalization.Format("Unable to load appearance. Kept current appearance.\n{0}", ex.Message), AppLocalization.Text("CodexBall Appearance"));
        }
    }
    private void PlayAction(CompanionAction action, bool force = false)
    {
        if (_player is null) return;
        if (!force && (!IsCompanionMode || !IsVisible || _isHiddenAtEdge)) return;
        if (!force && _isDragging && action != CompanionAction.Drag) return;
        // Status events do not interrupt direct user interaction or accumulate in a queue.
        if (!force && action is CompanionAction.QuotaLow or CompanionAction.QuotaRecovered
            && _action is CompanionAction.Pat or CompanionAction.Drag) return;
        _action = _skin!.Definition.Live2DModel is not null || _skin.Definition.Actions.ContainsKey(action.ToString()) ? action : CompanionAction.Idle;
        _player.Play(action);
    }

    private void OnCompanionReaction(object? sender, CompanionAction action) => PlayAction(action);

    private void UpdatePlayback()
    {
        _sleepTimer.Stop();
        if (!IsVisible || !IsCompanionMode || _isHiddenAtEdge)
        {
            _player?.Pause();
            UsagePopup.IsOpen = false;
        }
        else
        {
            PlayAction(CompanionAction.Idle, force: true);
            _player?.Resume();
            _sleepTimer.Start();
        }
    }

    private void RecordInteraction()
    {
        _sleepTimer.Stop();
        if (IsCompanionMode && IsVisible && !_isHiddenAtEdge) _sleepTimer.Start();
    }

    private void ClampToWorkArea()
    {
        var area = CurrentWorkArea();
        Left = Math.Clamp(double.IsFinite(Left) ? Left : area.Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(double.IsFinite(Top) ? Top : area.Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    private Rect CurrentWorkArea()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return SystemParameters.WorkArea;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var transform = System.Windows.PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? System.Windows.Media.Matrix.Identity;
        var rect = screen.WorkingArea;
        var start = transform.Transform(new System.Windows.Point(rect.Left, rect.Top));
        var end = transform.Transform(new System.Windows.Point(rect.Right, rect.Bottom));
        return new Rect(start, end);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(RepositionForDisplay);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(RepositionForDisplay);
    }

    private void RepositionForDisplay()
    {
        if (_settings.EdgeHideEnabled) EnterEdgeHideMode(animate: false);
        else { ClampToWorkArea(); CaptureVisiblePosition(); }
    }
}
