using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodexBall.App.Companion;
using CodexBall.App.Services;
using CodexBall.Core.Companion;
using CodexBall.Core.Utils;
using CodexBall.Rendering;

namespace CodexBall.Desktop;

internal sealed class WidgetWindow : Window
{
    private readonly StatusBallViewModel _viewModel;
    private readonly SettingsService _store;
    public AppSettings Settings { get; }
    private readonly Grid _root = new();
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly BallControl _ring;
    private readonly TextBlock _center = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.SemiBold, IsHitTestVisible = false };
    private readonly Border _usage = new() { Height = 4, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Border _edgeHandle = new() { IsVisible = false, Background = new SolidColorBrush(Color.Parse("#D9222830")), Child = new Border() };
    private HideEdge _hideEdge;
    private long _suppressPeekUntil;
    private readonly Popup _details = new() { IsLightDismissEnabled = true, Placement = PlacementMode.Right };
    private readonly DispatcherTimer _peek = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _sleep = new() { Interval = TimeSpan.FromMinutes(3) };
    private ICompanionPlayer? _player;
    private LoadedSkin? _skin;
    private PixelPoint _visible;
    private bool _restoring, _closed;
    private bool _edgeHidden, _dragging, _pressedOnBar, _pressedOnHead, _initializing = true;
    private Point _pressed;
    private PixelPoint _origin;
    private CompanionAction _action;
    public string? LastError { get; private set; }
    public string SelectedSkinId => Settings.SkinId;
    public bool IsCompanion => _player is not null;
    public bool IsEdgeHidden => _edgeHidden;
    public Live2DCompanionPlayer? Player => _player as Live2DCompanionPlayer;
    public event Action? StateChanged;

    public WidgetWindow(StatusBallViewModel viewModel, SettingsService store, AppSettings settings)
    {
        _viewModel = viewModel; _store = store; Settings = settings;
        WindowDecorations = WindowDecorations.None; Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; ShowActivated = false; CanResize = false;
        Topmost = settings.AlwaysOnTop; Opacity = settings.Opacity; Title = "CodexBall";
        _ring = new BallControl(viewModel, () => Settings.ShowResetRing) { Width = 64, Height = 64 };
        Content = _root; _details.PlacementTarget = this;
        _details.Closed += (_, _) => ApplyVisibility();
        BuildRing();
        _peek.Tick += (_, _) => { if (!IsPointerOver && Settings.EdgeHideEnabled) HideAtEdge(); };
        _sleep.Tick += (_, _) => { _sleep.Stop(); Play(CompanionAction.Sleep); };
        PointerEntered += (_, _) => { if (Environment.TickCount64 >= _suppressPeekUntil) Reveal(); _peek.Stop(); };
        PointerExited += (_, _) => { _player?.SetLookTarget(0, 0); if (Settings.EdgeHideEnabled) _peek.Start(); };
        PointerPressed += Pressed; PointerMoved += Moved; PointerReleased += Released;
        PointerCaptureLost += async (_, _) => { if (_dragging && !_closed) { _dragging = false; CompleteDrag(); await SaveAsync(); } };
        viewModel.PropertyChanged += (_, _) => UpdateData();
        viewModel.CodexActivityChanged += (_, _) => ApplyVisibility();
        viewModel.CompanionReaction += (_, action) => Play(action);
        AppLocalization.Current.PropertyChanged += (_, _) => { UpdateData(); UpdateDetails(); };
        PositionChanged += (_, _) => { if (!_initializing && !_edgeHidden && !_dragging) _visible = Position; };
        Opened += (_, _) => InitializeAppearance();
        Closed += (_, _) => { _closed = true; _peek.Stop(); _sleep.Stop(); ReleasePlayer(); };
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdatePlayback(); };
        ScalingChanged += (_, _) => { if (!_initializing) Dispatcher.UIThread.Post(Reposition); };
        Screens.Changed += (_, _) => { if (!_initializing) Dispatcher.UIThread.Post(Reposition); };
        UpdateData();
    }

    private void InitializeAppearance()
    {
        if (!_initializing) return;
        _initializing = false; _restoring = true;
        var area = (Screens.Primary ?? Screens.All.First()).WorkingArea;
        Position = Settings.Left is { } left && Settings.Top is { } top
            ? new PixelPoint((int)left, (int)top) : new PixelPoint(area.Right - 130, area.Y + 80);
        _visible = Position;
        var id = Settings.SkinId;
        Settings.SkinId = SkinCatalog.BallId; Settings.CompanionMode = false;
        if (SkinCatalog.Discover(out _).Any(s => s.Id == id)) SelectAppearance(id, persist: false);
        Clamp(); _visible = Position; _restoring = false;
        ApplyVisibility();
        if (!_initializing && Settings.EdgeHideEnabled && IsVisible) HideAtEdge();
        _ = SaveAsync();
    }
    private void BuildRing()
    {
        _root.Children.Clear(); Width = Height = 64;
        _root.Children.Add(_ring); _root.Children.Add(_center); _root.Children.Add(_edgeHandle); _root.Children.Add(_details);
    }
    private void ReleasePlayer()
    {
        if (_image.Parent is Panel parent) parent.Children.Remove(_image);
        if (_usage.Parent is Border oldBar) oldBar.Child = null;
        _image.Source = null; _player?.Dispose(); _player = null; _skin = null;
    }
    public bool SelectAppearance(string id, bool persist = true)
    {
        var entry = SkinCatalog.Discover(out _).FirstOrDefault(s => s.Id == id);
        if (entry is null) return false;
        LastError = null;
        try
        {
            // Complete loading before releasing the usable current appearance.
            var skin = entry.Directory is null ? null : SkinLoader.Load(entry.Directory);
            ICompanionPlayer? next = skin is null ? null : skin.Definition.Live2DModel is null ? new PngCompanionPlayer(skin) : new Live2DCompanionPlayer(skin, Program.ForceCore);
            Reveal(); _details.IsOpen = false; ReleasePlayer();
            Settings.SkinId = id; Settings.CompanionMode = next is not null;
            _player = next; _skin = skin; _action = CompanionAction.Idle;
            if (next is null) { if (persist) Settings.ShowResetRing = false; BuildRing(); }
            else
            {
                Width = skin!.Definition.Width; Height = skin.Definition.Height + 20;
                _image.Width = next.ImageBounds.Width; _image.Height = next.ImageBounds.Height; _image.Source = next.Image;
                Canvas.SetLeft(_image, next.ImageBounds.X); Canvas.SetTop(_image, next.ImageBounds.Y);
                var canvas = new Canvas { Width = Width, Height = Height, Background = Brushes.Transparent };
                canvas.Children.Add(_image);
                var bar = new Border { Width = Math.Max(80, Width - 40), Height = 4, Background = new SolidColorBrush(Color.Parse("#228C96A0")), Child = _usage };
                Canvas.SetLeft(bar, 20); Canvas.SetTop(bar, skin.Definition.Height + 8 + skin.Definition.UsageBarOffsetY);
                bar.PointerPressed += (_, e) => { _pressedOnBar = true; };
                canvas.Children.Add(bar); _root.Children.Clear(); _root.Children.Add(canvas); _root.Children.Add(_edgeHandle); _root.Children.Add(_details);
                next.FrameChanged += (_, _) => { _image.Source = next.Image; _image.Width = next.ImageBounds.Width; _image.Height = next.ImageBounds.Height; Canvas.SetLeft(_image, next.ImageBounds.X); Canvas.SetTop(_image, next.ImageBounds.Y); _image.InvalidateVisual(); };
                next.Completed += (_, _) => { if (_action != CompanionAction.Drag) Play(CompanionAction.Idle, true); };
                next.Failed += (_, error) => Dispatcher.UIThread.Post(() =>
                {
                    if (!ReferenceEquals(_player, next)) return;
                    SelectAppearance(SkinCatalog.BallId);
                    LastError = error.ToString(); DesktopApp.ShowMessage(AppLocalization.Format("Live2D rendering stopped. Restored CodexBall.\n{0}", error.Message));
                });
            }
            Clamp(); _visible = Position; UpdateData(); ApplyVisibility(); UpdatePlayback();
            if (!_initializing && Settings.EdgeHideEnabled && IsVisible) HideAtEdge();
            if (persist) _ = SaveAsync(); StateChanged?.Invoke(); return true;
        }
        catch (Exception error)
        {
            LastError = error.ToString();
            if (!Program.Smoke) DesktopApp.ShowMessage(AppLocalization.Format("Unable to load appearance. Kept current appearance.\n{0}", error.Message));
            return false;
        }
    }
    public void CycleAppearance()
    {
        var entries = SkinCatalog.Discover(out _); var index = entries.ToList().FindIndex(e => e.Id == Settings.SkinId);
        SelectAppearance(entries[(index + 1) % entries.Count].Id);
    }
    public void SetLanguage(string language)
    {
        Settings.Language = language; AppLocalization.Current.Apply(language); _ = SaveAsync(); StateChanged?.Invoke();
    }
    public void SetTopmost(bool enabled) { Topmost = Settings.AlwaysOnTop = enabled; _ = SaveAsync(); StateChanged?.Invoke(); }
    public void SetEdgeHide(bool enabled)
    {
        Settings.EdgeHideEnabled = enabled;
        if (enabled && IsVisible) HideAtEdge(); else Reveal();
        _ = SaveAsync(); StateChanged?.Invoke();
    }
    public void ToggleEdgeHide() => SetEdgeHide(!Settings.EdgeHideEnabled);
    public void HideAtEdge()
    {
        if (_restoring || _closed || _edgeHidden || _dragging || !IsVisible) return;
        _peek.Stop(); _details.IsOpen = false;
        var area = (Screens.ScreenFromWindow(this) ?? Screens.Primary)!.WorkingArea;
        var placement = EdgeHideGeometry.Calculate(Position.X, Position.Y, Width * RenderScaling, Height * RenderScaling, area.X, area.Y, area.Width, area.Height, 3 * RenderScaling);
        _visible = new PixelPoint((int)placement.VisibleLeft, (int)placement.VisibleTop);
        _hideEdge = placement.Edge; _edgeHidden = true; _edgeHandle.IsVisible = true; _suppressPeekUntil = Environment.TickCount64 + 500;
        var side = placement.Edge is HideEdge.Left or HideEdge.Right;
        _edgeHandle.Width = side ? 3 : Width; _edgeHandle.Height = side ? Height : 3;
        _edgeHandle.HorizontalAlignment = placement.Edge == HideEdge.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _edgeHandle.VerticalAlignment = placement.Edge == HideEdge.Top ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        UpdateEdgeFill();
        Position = new PixelPoint((int)placement.HiddenLeft, (int)placement.HiddenTop); UpdatePlayback();
    }
    public void Reveal()
    {
        _peek.Stop();
        if (!_edgeHidden) return;
        Position = _visible; _edgeHidden = false; _edgeHandle.IsVisible = false; UpdatePlayback();
    }
    private void Reposition() { if (_closed) return; Reveal(); Clamp(); _visible = Position; if (Settings.EdgeHideEnabled) HideAtEdge(); }
    private void Clamp()
    {
        var area = (Screens.ScreenFromWindow(this) ?? Screens.Primary)!.WorkingArea;
        Position = new PixelPoint(Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - (int)(Width * RenderScaling))), Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - (int)(Height * RenderScaling))));
    }
    public void ApplyVisibility()
    {
        if (_restoring || _closed) return;
        if (IsCompanion || _viewModel.IsCodexActive) { if (!IsVisible) { Show(); if (!_initializing && Settings.EdgeHideEnabled) HideAtEdge(); } }
        else { _details.IsOpen = false; Hide(); }
        UpdatePlayback();
    }
    private void UpdatePlayback()
    {
        _sleep.Stop();
        if (_closed || _restoring) return;
        if (!IsVisible || _edgeHidden) _player?.Pause();
        else if (_player is not null) { _player.Resume(); _sleep.Start(); }
    }
    private void Play(CompanionAction action, bool force = false)
    {
        if (_player is null || !IsVisible || _edgeHidden) return;
        if (!force && _dragging && action != CompanionAction.Drag) return;
        if (!force && action is CompanionAction.QuotaLow or CompanionAction.QuotaRecovered && _action is CompanionAction.Drag or CompanionAction.Pat) return;
        _action = action; _player.Play(action);
    }
    private void Interaction() { _sleep.Stop(); if (IsCompanion && !_edgeHidden) _sleep.Start(); }
    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Reveal(); _pressed = e.GetPosition(this); _origin = Position; _dragging = false; Interaction();
        var head = _skin?.Definition.Head; _pressedOnHead = head is not null && _pressed.X >= head.X && _pressed.X <= head.X + head.Width && _pressed.Y >= head.Y && _pressed.Y <= head.Y + head.Height;
        e.Pointer.Capture(this);
    }
    private void Moved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this); _player?.SetLookTarget(p.X / Width * 2 - 1, 1 - p.Y / (_skin?.Definition.Height ?? Height) * 2);
        if (e.Pointer.Captured != this) return;
        var screen = this.PointToScreen(p); var dx = screen.X - _origin.X - (int)(_pressed.X * RenderScaling); var dy = screen.Y - _origin.Y - (int)(_pressed.Y * RenderScaling);
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 5) return;
        if (!_dragging) { Settings.EdgeHideEnabled = false; _details.IsOpen = false; Play(CompanionAction.Drag); StateChanged?.Invoke(); }
        _dragging = true; _player?.SetDragImpulse(dx / RenderScaling); Position = new PixelPoint(_origin.X + dx, _origin.Y + dy);
    }
    private async void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured != this) return;
        var dragged = _dragging; _dragging = false; e.Pointer.Capture(null);
        if (dragged) CompleteDrag();
        else if (IsCompanion) { if (_pressedOnBar) ShowDetails(); else if (_pressedOnHead) Play(CompanionAction.Pat); }
        else { Settings.ShowResetRing = !Settings.ShowResetRing; _ring.InvalidateVisual(); }
        _pressedOnBar = false; if (Settings.EdgeHideEnabled && !_edgeHidden) _peek.Start(); await SaveAsync();
    }
    internal void CompleteDrag()
    {
        if (_closed) return;
        var area = (Screens.ScreenFromWindow(this) ?? Screens.Primary)!.WorkingArea;
        var autoHide = EdgeHideGeometry.IsAtWorkAreaEdge(Position.X, Position.Y,
            Width * RenderScaling, Height * RenderScaling, area.X, area.Y, area.Width, area.Height, 12 * RenderScaling);
        Clamp(); _visible = Position;
        Play(CompanionAction.Drop, true);
        Interaction();
        if (autoHide) SetEdgeHide(true);
    }
    public async Task SaveAsync()
    {
        if (_initializing || _restoring) return;
        var point = _edgeHidden ? _visible : Position;
        Settings.Left = point.X; Settings.Top = point.Y;
        try { await _store.SaveAsync(Settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { DesktopApp.Log(e); }
    }
    private void UpdateData()
    {
        _center.Text = _viewModel.CenterText; _ring.InvalidateVisual();
        _usage.Background = _viewModel.RingBrush; _usage.Width = Math.Max(0, (Width - 40) * _viewModel.Progress / 100);
        UpdateEdgeFill();
        ToolTip.SetTip(this, _viewModel.TooltipText); if (_details.IsOpen) UpdateDetails();
    }
    private void UpdateEdgeFill()
    {
        var fill = (Border)_edgeHandle.Child!; var side = _hideEdge is HideEdge.Left or HideEdge.Right;
        fill.Background = _viewModel.RingBrush; fill.Width = side ? 3 : Width * _viewModel.Progress / 100; fill.Height = side ? Height * _viewModel.Progress / 100 : 3;
        fill.HorizontalAlignment = HorizontalAlignment.Left; fill.VerticalAlignment = VerticalAlignment.Bottom;
    }
    public void ShowDetails() { Reveal(); if (!IsVisible) Show(); UpdateDetails(); _details.IsOpen = true; }
    private void UpdateDetails()
    {
        var stack = new StackPanel { Spacing = 8 };
        Avalonia.Controls.Documents.TextElement.SetForeground(stack, Brushes.White);
        stack.Children.Add(new TextBlock { Text = AppLocalization.Text("Codex usage"), FontWeight = FontWeight.Bold });
        stack.Children.Add(new TextBlock { Text = $"{_viewModel.ShortWindowLabel}  {_viewModel.ShortWindowPercent}" });
        stack.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = _viewModel.ShortWindowProgress, Height = 4 });
        stack.Children.Add(new TextBlock { Text = _viewModel.ShortWindowReset });
        stack.Children.Add(new TextBlock { Text = $"{_viewModel.LongWindowLabel}  {_viewModel.LongWindowPercent}" });
        stack.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = _viewModel.LongWindowProgress, Height = 4 });
        stack.Children.Add(new TextBlock { Text = _viewModel.LongWindowReset });
        stack.Children.Add(new TextBlock { Text = _viewModel.LastUpdatedText, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 });
        _details.Child = new Border { Background = new SolidColorBrush(Color.Parse("#F2232830")), Padding = new Thickness(16), CornerRadius = new CornerRadius(12), Child = stack, MinWidth = 250 };
    }
}
