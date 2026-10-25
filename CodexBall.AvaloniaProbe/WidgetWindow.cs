using CodexBall.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using CodexBall.Core.Companion;
using CodexBall.Core.Utils;

namespace CodexBall.AvaloniaProbe;

internal sealed class WidgetWindow : Window
{
    private Live2DCompanionPlayer? _player;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private PixelPoint? _visible;
    private Point _pressed;
    private PixelPoint _origin;
    private bool _dragging, _dual;
    public static bool KoharuAvailable { get { try { SkinLoader.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "Skins", "local-koharu")); return true; } catch { return false; } } }
    public bool IsCompanion => _player is not null;
    public bool IsEdgeHidden => _visible.HasValue;
    public Live2DCompanionPlayer? Player => _player;
    public Action<string>? Report;
    public string? LastError { get; private set; }
    public WidgetWindow()
    {
        Title = AppLocalization.Title;
        WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; ShowActivated = false; CanResize = false; Topmost = true;
        ShowRing();
        Opened += (_, _) => { var r = Screens.Primary?.WorkingArea; if (r.HasValue) Position = new PixelPoint(r.Value.X + 100, r.Value.Y + 160); };
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Reveal(); _pressed = e.GetPosition(this); _origin = Position; _dragging = false; e.Pointer.Capture(this);
        };
        PointerMoved += (_, e) =>
        {
            var p = e.GetPosition(this);
            _player?.SetLookTarget(p.X / Width * 2 - 1, 1 - p.Y / Height * 2);
            if (e.Pointer.Captured != this) return;
            var screen = this.PointToScreen(p); var start = new PixelPoint(_origin.X + (int)(_pressed.X * RenderScaling), _origin.Y + (int)(_pressed.Y * RenderScaling));
            var dx = screen.X - start.X; var dy = screen.Y - start.Y;
            if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 5) return;
            if (!_dragging) _player?.Play(CompanionAction.Drag);
            _dragging = true; _player?.SetDragImpulse(dx / RenderScaling);
            Position = new PixelPoint(_origin.X + dx, _origin.Y + dy);
        };
        PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured != this) return;
            e.Pointer.Capture(null);
            if (_dragging) { _player?.Play(CompanionAction.Drop); ClampPosition(); }
            else if (_player is not null) _player.Play(CompanionAction.Pat);
            else { _dual = !_dual; ShowRing(); }
        };
        PointerEntered += (_, _) => Reveal();
        PointerExited += (_, _) => _player?.SetLookTarget(0, 0);
        Closed += (_, _) => ReleasePlayer();
    }
    private void ReleasePlayer()
    {
        if (_image.Parent is Panel oldPanel) oldPanel.Children.Remove(_image);
        _image.Source = null;
        _player?.Dispose(); _player = null;
    }
    public void ShowRing()
    {
        Reveal(); ReleasePlayer(); Width = Height = 64;
        var canvas = new Canvas { Width = 64, Height = 64, Background = Brushes.Transparent };
        canvas.Children.Add(new Ellipse { Width = 56, Height = 56, Margin = new Thickness(4), Fill = new SolidColorBrush(Color.Parse("#DE162331")), Stroke = Brushes.MediumTurquoise, StrokeThickness = 4 });
        if (_dual) canvas.Children.Add(new Ellipse { Width = 40, Height = 40, Margin = new Thickness(12), Stroke = Brushes.HotPink, StrokeThickness = 3 });
        Content = canvas;
    }
    public void ToggleAppearance()
    {
        if (IsCompanion) { ShowRing(); ClampPosition(); return; }
        LastError = null;
        try
        {
            Reveal();
            var skin = SkinLoader.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "Skins", "local-koharu"));
            var player = new Live2DCompanionPlayer(skin, Program.UseCoreProfile);
            _player = player;
            Width = skin.Definition.Width; Height = skin.Definition.Height + 20;
            _image.Width = Width; _image.Height = skin.Definition.Height; _image.Source = player.Image;
            var layout = new StackPanel(); layout.Children.Add(_image);
            var bar = new Border { Height = 4, Width = 180, Margin = new Thickness(0, 8), Background = Brushes.MediumTurquoise };
            bar.PointerPressed += (_, e) => { Report?.Invoke(AppLocalization.Text("验证数据：剩余 75%，未连接 Codex。", "Demo data: 75% remaining; Codex is not connected.")); e.Handled = true; };
            layout.Children.Add(bar); Content = layout;
            player.FrameChanged += (_, _) => _image.InvalidateVisual();
            player.Failed += (_, error) => { LastError = error.ToString(); ShowRing(); Report?.Invoke(LastError); };
            player.Resume(); ClampPosition();
            Report?.Invoke(player.RendererDescription);
        }
        catch (Exception error) { LastError = error.ToString(); ShowRing(); Report?.Invoke(LastError); }
    }
    public void ToggleEdgeHide()
    {
        if (IsEdgeHidden) { Reveal(); return; }
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var r = screen.WorkingArea;
        var p = EdgeHideGeometry.Calculate(Position.X, Position.Y, Width * RenderScaling, Height * RenderScaling, r.X, r.Y, r.Width, r.Height, 6 * RenderScaling);
        _visible = new PixelPoint((int)p.VisibleLeft, (int)p.VisibleTop);
        Position = new PixelPoint((int)p.HiddenLeft, (int)p.HiddenTop);
        _player?.Pause();
    }
    public void Reveal()
    {
        if (!_visible.HasValue) return;
        Position = _visible.Value; _visible = null; _player?.Resume();
    }
    private void ClampPosition()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var r = screen.WorkingArea;
        Position = new PixelPoint(Math.Clamp(Position.X, r.X, Math.Max(r.X, r.Right - (int)(Width * RenderScaling))), Math.Clamp(Position.Y, r.Y, Math.Max(r.Y, r.Bottom - (int)(Height * RenderScaling))));
    }
}
