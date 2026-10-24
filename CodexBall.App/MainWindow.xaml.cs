using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;
using CodexBall.Core.Utils;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CodexBall.App;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty ShowResetRingProperty =
        DependencyProperty.Register(nameof(ShowResetRing), typeof(bool), typeof(MainWindow),
            new PropertyMetadata(false));

    private const int HideHotKeyId = 0x4342;
    private const int AppearanceHotKeyId = 0x4343;
    private const int ModAlt = 0x0001;
    private const int ModNoRepeat = 0x4000;
    private const int VirtualKeyC = 0x43;
    private const int VirtualKeyH = 0x48;
    private const int WmHotKey = 0x0312;
    private const double VisibleStrip = 3;

    private readonly StatusBallViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _peekTimer;
    private long _suppressEdgePeekUntil;
    private Point _mouseDownPosition;
    private bool _isDragging;
    private bool _dragGesture;
    private double _dragPreviousLeft;
    private bool _manualDrag;
    private bool _isHiddenAtEdge;
    private bool _isPeeking;
    private double _visibleLeft;
    private double _visibleTop;
    private double _hiddenLeft;
    private double _hiddenTop;
    private HwndSource? _hwndSource;

    public MainWindow(StatusBallViewModel viewModel, SettingsService settingsService, AppSettings settings)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();
        ShowResetRing = settings.ShowResetRing;

        _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer.Stop();
            if (_settings.EdgeHideEnabled && _isPeeking)
            {
                SlideToHidden();
            }
        };

        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;
        if (settings.Left is not null && settings.Top is not null)
        {
            Left = settings.Left.Value;
            Top = settings.Top.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            PlaceAtDefaultTopRight();
        }

        Loaded += (_, _) =>
        {
            ClampToWorkArea();
            CaptureVisiblePosition();
            if (_settings.EdgeHideEnabled)
            {
                EnterEdgeHideMode(animate: false);
            }
        };
        MouseEnter += (_, _) => PeekFromEdge();
        InitializeCompanion();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);
        var handle = new WindowInteropHelper(this).Handle;
        RegisterHotKey(handle, HideHotKeyId, ModAlt | ModNoRepeat, VirtualKeyH);
        RegisterHotKey(handle, AppearanceHotKeyId, ModAlt | ModNoRepeat, VirtualKeyC);
    }

    protected override async void OnClosed(EventArgs e)
    {
        _peekTimer.Stop();
        _sleepTimer.Stop();
        _player?.Dispose();
        _viewModel.CompanionReaction -= OnCompanionReaction;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            UnregisterHotKey(_hwndSource.Handle, HideHotKeyId);
            UnregisterHotKey(_hwndSource.Handle, AppearanceHotKeyId);
        }

        await SavePositionAsync();
        base.OnClosed(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (UsagePopup.IsOpen && UsagePopup.Child is FrameworkElement panel && panel.IsMouseOver)
        {
            base.OnMouseLeftButtonDown(e);
            return;
        }
        if (_settings.EdgeHideEnabled)
        {
            PeekFromEdge();
        }

        _peekTimer.Stop();
        _mouseDownPosition = e.GetPosition(this);
        _isDragging = false;
        _dragGesture = false;
        RecordInteraction();
        var barPoint = e.GetPosition(UsageBar);
        _usageBarClick = IsCompanionMode && barPoint.X >= 0 && barPoint.Y >= 0
            && barPoint.X <= UsageBar.ActualWidth && barPoint.Y <= UsageBar.ActualHeight;
        var head = _skin?.Definition.Head;
        var characterPoint = e.GetPosition(CharacterCanvas);
        _headClick = IsCompanionMode && head is not null && characterPoint.X >= head.X
            && characterPoint.X <= head.X + head.Width && characterPoint.Y >= head.Y
            && characterPoint.Y <= head.Y + head.Height;
        CaptureMouse();
        base.OnMouseLeftButtonDown(e);
    }

    protected override async void OnMouseMove(MouseEventArgs e)
    {
        if (IsCompanionMode && _player is Companion.IInteractiveCompanionPlayer interactive)
        {
            var pointer = e.GetPosition(CharacterCanvas);
            interactive.SetLookTarget((pointer.X / CharacterCanvas.Width - .5) * 2, (.5 - pointer.Y / CharacterCanvas.Height) * 2);
        }
        if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured)
        {
            base.OnMouseMove(e);
            return;
        }

        var current = e.GetPosition(this);
        if (_manualDrag)
        {
            Left += current.X - _mouseDownPosition.X;
            Top += current.Y - _mouseDownPosition.Y;
            return;
        }
        if (!_isDragging && (Math.Abs(current.X - _mouseDownPosition.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - _mouseDownPosition.Y) >= SystemParameters.MinimumVerticalDragDistance))
        {
            _isDragging = true;
            _dragGesture = true;
            _settings.EdgeHideEnabled = false;
            ShowVisible(animate: false);
            UsagePopup.IsOpen = false;
            PlayAction(CodexBall.Core.Companion.CompanionAction.Drag);
            if (_player is Companion.IInteractiveCompanionPlayer && IsCompanionMode)
            {
                // Keep the WPF dispatcher/render loop active while dragging a live model.
                _manualDrag = true;
                Left += current.X - _mouseDownPosition.X;
                Top += current.Y - _mouseDownPosition.Y;
                return;
            }
            ReleaseMouseCapture();
            try { DragMove(); }
            catch (InvalidOperationException) { /* Button can be released before the native drag starts. */ }
            finally
            {
                _isDragging = false;
                await FinishDragAsync();
            }
        }

        base.OnMouseMove(e);
    }

    protected override async void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_manualDrag)
        {
            await FinishManualDragAsync();
            base.OnMouseLeftButtonUp(e);
            return;
        }
        var hadCapture = IsMouseCaptured;
        if (hadCapture)
        {
            ReleaseMouseCapture();
        }

        if (_dragGesture || !hadCapture)
        {
            // DragMove finishes and saves in OnMouseMove; never turn its release into a click.
        }
        else if (_settings.EdgeHideEnabled && _isPeeking && !IsCompanionMode)
        {
            _peekTimer.Start();
        }
        else
        {
            if (IsCompanionMode)
            {
                if (_usageBarClick) UsagePopup.IsOpen = !UsagePopup.IsOpen;
                else if (_headClick) PlayAction(CodexBall.Core.Companion.CompanionAction.Pat);
            }
            else await ToggleRingModeAsync();
        }

        base.OnMouseLeftButtonUp(e);
        if (_isPeeking) _peekTimer.Start();
    }

    protected override async void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_manualDrag) await FinishManualDragAsync();
        base.OnLostMouseCapture(e);
    }

    private async Task FinishManualDragAsync()
    {
        _manualDrag = false;
        _isDragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        await FinishDragAsync();
    }

    private async Task FinishDragAsync()
    {
        var area = CurrentWorkArea();
        // Check the released position before clamping removes the crossed-edge signal.
        var hideAtEdge = EdgeHideGeometry.IsBeyondWorkArea(Left, Top, Width, Height,
            area.Left, area.Top, area.Width, area.Height);
        ClampToWorkArea();
        CaptureVisiblePosition();
        PlayAction(CodexBall.Core.Companion.CompanionAction.Drop, force: true);
        RecordInteraction();
        if (hideAtEdge)
        {
            // The released pointer may still overlap the reveal strip during the slide.
            _suppressEdgePeekUntil = Environment.TickCount64 + 1000;
            await SetEdgeHideModeAsync(true);
            return;
        }
        await SavePositionAsync();
    }

    public void ShowForCodex()
    {
        if (!IsVisible)
        {
            Show();
        }
        UpdatePlayback();
    }

    public void HideForCodex()
    {
        if (IsCompanionMode) return;
        _peekTimer.Stop();
        Hide();
    }

    public bool IsAlwaysOnTopEnabled => Topmost;

    public bool IsEdgeHideEnabled => _settings.EdgeHideEnabled;

    public bool ShowResetRing
    {
        get => (bool)GetValue(ShowResetRingProperty);
        set => SetValue(ShowResetRingProperty, value);
    }

    public string LanguagePreference => _settings.Language;

    public async Task SetLanguageAsync(string language)
    {
        AppLocalization.Current.Apply(language);
        _settings.Language = AppLocalization.Current.Preference;
        await SavePositionAsync();
    }

    public async Task SetAlwaysOnTopAsync(bool enabled)
    {
        Topmost = enabled;
        _settings.AlwaysOnTop = enabled;
        await _settingsService.SaveAsync(_settings);
    }

    private async Task ToggleRingModeAsync()
    {
        ShowResetRing = !ShowResetRing;
        _settings.ShowResetRing = ShowResetRing;
        await SavePositionAsync();
    }

    private void PlaceAtDefaultTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Right - Width - 40;
        Top = workArea.Top + 40;
    }

    public async Task SetEdgeHideModeAsync(bool enabled)
    {
        if (enabled)
        {
            EnterEdgeHideMode(animate: true);
        }
        else
        {
            ExitEdgeHideMode();
        }

        _settings.EdgeHideEnabled = enabled;
        await SavePositionAsync();
        await _viewModel.RefreshAsync();
    }

    private void EnterEdgeHideMode(bool animate)
    {
        var workArea = CurrentWorkArea();
        if (!_isHiddenAtEdge && !_isPeeking)
        {
            CaptureVisiblePosition();
        }

        var placement = EdgeHideGeometry.Calculate(_visibleLeft, _visibleTop, Width, Height,
            workArea.Left, workArea.Top, workArea.Width, workArea.Height, VisibleStrip);
        _visibleLeft = placement.VisibleLeft;
        _visibleTop = placement.VisibleTop;
        _hiddenLeft = placement.HiddenLeft;
        _hiddenTop = placement.HiddenTop;
        // WPF's ProgressBar sizes PART_Indicator along X; rotate the complete
        // horizontal control so the same fill calculation also works at side edges.
        EdgeUsageProgress.LayoutTransform = placement.Edge is HideEdge.Left or HideEdge.Right
            ? new System.Windows.Media.RotateTransform(-90) : System.Windows.Media.Transform.Identity;
        EdgeRevealHandle.Width = placement.Edge is HideEdge.Left or HideEdge.Right ? VisibleStrip : double.NaN;
        EdgeRevealHandle.Height = placement.Edge is HideEdge.Top or HideEdge.Bottom ? VisibleStrip : double.NaN;
        EdgeRevealHandle.HorizontalAlignment = placement.Edge switch
        {
            HideEdge.Left => System.Windows.HorizontalAlignment.Right,
            HideEdge.Right => System.Windows.HorizontalAlignment.Left,
            _ => System.Windows.HorizontalAlignment.Stretch
        };
        EdgeRevealHandle.VerticalAlignment = placement.Edge switch
        {
            HideEdge.Top => VerticalAlignment.Bottom,
            HideEdge.Bottom => VerticalAlignment.Top,
            _ => VerticalAlignment.Stretch
        };
        SlideToHidden(animate);
    }

    private void ExitEdgeHideMode()
    {
        _peekTimer.Stop();
        _settings.EdgeHideEnabled = false;
        ShowVisible(animate: true);
    }

    private void PeekFromEdge()
    {
        if (Environment.TickCount64 < _suppressEdgePeekUntil) return;
        if (!_settings.EdgeHideEnabled || (!_isHiddenAtEdge && !_isPeeking))
        {
            return;
        }

        _peekTimer.Stop();
        _isPeeking = true;
        _isHiddenAtEdge = false;
        EdgeRevealHandle.Visibility = Visibility.Collapsed;
        UpdatePlayback();
        AnimateWindowTo(_visibleLeft, _visibleTop, TimeSpan.FromMilliseconds(420), () =>
        {
            if (_settings.EdgeHideEnabled)
            {
                _peekTimer.Start();
            }
        });
    }

    private void SlideToHidden(bool animate = true)
    {
        _isPeeking = false;
        _isHiddenAtEdge = true;
        EdgeRevealHandle.Visibility = Visibility.Visible;
        UpdatePlayback();

        if (animate)
        {
            AnimateWindowTo(_hiddenLeft, _hiddenTop, TimeSpan.FromMilliseconds(420));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = _hiddenLeft;
            Top = _hiddenTop;
        }
    }

    private void ShowVisible(bool animate)
    {
        _peekTimer.Stop();
        _isPeeking = false;
        _isHiddenAtEdge = false;
        EdgeRevealHandle.Visibility = Visibility.Collapsed;
        UpdatePlayback();

        if (animate)
        {
            AnimateWindowTo(_visibleLeft, _visibleTop, TimeSpan.FromMilliseconds(260));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = _visibleLeft;
            Top = _visibleTop;
        }
    }

    private void CaptureVisiblePosition()
    {
        if (_isHiddenAtEdge || _isPeeking)
        {
            return;
        }

        _visibleLeft = Left;
        _visibleTop = Top;
    }

    private void AnimateWindowTo(double left, double top, TimeSpan duration, Action? completed = null)
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var leftAnimation = new DoubleAnimation(left, duration) { EasingFunction = easing };
        var topAnimation = new DoubleAnimation(top, duration) { EasingFunction = easing };

        if (completed is not null)
        {
            leftAnimation.Completed += (_, _) => completed();
        }

        BeginAnimation(LeftProperty, leftAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(TopProperty, topAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) // WM_MOUSEACTIVATE: deliver clicks without stealing keyboard focus.
        {
            handled = true;
            return new IntPtr(3); // MA_NOACTIVATE
        }
        if (message == WmHotKey && wParam.ToInt32() == HideHotKeyId)
        {
            _ = SetEdgeHideModeAsync(!_settings.EdgeHideEnabled);
            handled = true;
        }
        else if (message == WmHotKey && wParam.ToInt32() == AppearanceHotKeyId)
        {
            _ = CycleAppearanceAsync();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private async Task SavePositionAsync()
    {
        _settings.Left = _visibleLeft;
        _settings.Top = _visibleTop;
        _settings.Opacity = Opacity;
        _settings.AlwaysOnTop = Topmost;
        await _settingsService.SaveAsync(_settings);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
