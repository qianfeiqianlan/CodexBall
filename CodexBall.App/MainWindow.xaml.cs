using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CodexBall.App;

public partial class MainWindow : Window
{
    private const int HotKeyId = 0x4342;
    private const int ModAlt = 0x0001;
    private const int VirtualKeyC = 0x43;
    private const int WmHotKey = 0x0312;
    private const double VisibleStrip = 6;

    private readonly StatusBallViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _peekTimer;
    private Point _mouseDownPosition;
    private bool _isDragging;
    private bool _isHiddenAtEdge;
    private bool _isPeeking;
    private double _visibleLeft;
    private double _visibleTop;
    private HwndSource? _hwndSource;
    private UsagePopup? _popup;

    public MainWindow(StatusBallViewModel viewModel, SettingsService settingsService, AppSettings settings)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();

        _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer.Stop();
            if (_settings.EdgeHideEnabled && _isPeeking && _popup is not { IsVisible: true })
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
            CaptureVisiblePosition();
            if (_settings.EdgeHideEnabled)
            {
                EnterEdgeHideMode(animate: false);
            }
        };
        MouseEnter += (_, _) => PeekFromEdge();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);
        RegisterHotKey(new WindowInteropHelper(this).Handle, HotKeyId, ModAlt, VirtualKeyC);
    }

    protected override async void OnClosed(EventArgs e)
    {
        _peekTimer.Stop();
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            UnregisterHotKey(_hwndSource.Handle, HotKeyId);
        }

        await SavePositionAsync();
        base.OnClosed(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_settings.EdgeHideEnabled)
        {
            PeekFromEdge();
        }

        _peekTimer.Stop();
        _mouseDownPosition = e.GetPosition(this);
        _isDragging = false;
        CaptureMouse();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured)
        {
            base.OnMouseMove(e);
            return;
        }

        var current = e.GetPosition(this);
        if (!_isDragging && (Math.Abs(current.X - _mouseDownPosition.X) >= 4 || Math.Abs(current.Y - _mouseDownPosition.Y) >= 4))
        {
            _isDragging = true;
            _settings.EdgeHideEnabled = false;
            ShowVisible(animate: false);
            ReleaseMouseCapture();
            DragMove();
        }

        base.OnMouseMove(e);
    }

    protected override async void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (_isDragging)
        {
            CaptureVisiblePosition();
            await SavePositionAsync();
        }
        else
        {
            TogglePopup();
        }

        base.OnMouseLeftButtonUp(e);
    }

    private void TogglePopup()
    {
        if (_settings.EdgeHideEnabled)
        {
            PeekFromEdge();
        }

        _peekTimer.Stop();
        if (_popup is { IsVisible: true })
        {
            _popup.Close();
            _popup = null;
            return;
        }

        _popup = new UsagePopup
        {
            DataContext = _viewModel,
            Owner = this,
            Left = Math.Min(_visibleLeft + Width + 8, SystemParameters.WorkArea.Right - 292),
            Top = _visibleTop
        };
        _popup.Closed += (_, _) =>
        {
            _popup = null;
            if (_settings.EdgeHideEnabled)
            {
                _peekTimer.Start();
            }
        };
        _popup.Show();
    }

    public void ShowForCodex()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideForCodex()
    {
        _popup?.Close();
        _popup = null;
        _peekTimer.Stop();
        Hide();
    }

    public bool IsAlwaysOnTopEnabled => Topmost;

    public bool IsEdgeHideEnabled => _settings.EdgeHideEnabled;

    public async Task SetAlwaysOnTopAsync(bool enabled)
    {
        Topmost = enabled;
        _settings.AlwaysOnTop = enabled;
        await _settingsService.SaveAsync(_settings);
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
        var workArea = SystemParameters.WorkArea;
        if (!_isHiddenAtEdge && !_isPeeking)
        {
            CaptureVisiblePosition();
        }

        _visibleLeft = workArea.Right - Width;
        _visibleTop = Clamp(_visibleTop, workArea.Top, workArea.Bottom - Height);
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
        if (!_settings.EdgeHideEnabled || (!_isHiddenAtEdge && !_isPeeking))
        {
            return;
        }

        _peekTimer.Stop();
        _isPeeking = true;
        _isHiddenAtEdge = false;
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
        var hiddenLeft = SystemParameters.WorkArea.Right - VisibleStrip;
        _isPeeking = false;
        _isHiddenAtEdge = true;

        if (animate)
        {
            AnimateWindowTo(hiddenLeft, _visibleTop, TimeSpan.FromMilliseconds(420));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = hiddenLeft;
            Top = _visibleTop;
        }
    }

    private void ShowVisible(bool animate)
    {
        _peekTimer.Stop();
        _isPeeking = false;
        _isHiddenAtEdge = false;

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
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            _ = SetEdgeHideModeAsync(!_settings.EdgeHideEnabled);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static double Clamp(double value, double minimum, double maximum)
        => Math.Min(Math.Max(value, minimum), maximum);

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
