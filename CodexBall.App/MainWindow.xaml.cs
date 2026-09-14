using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;

namespace CodexBall.App;

public partial class MainWindow : Window
{
    private readonly StatusBallViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private Point _mouseDownPosition;
    private bool _isDragging;
    private bool _isHiddenAtEdge;
    private double _visibleLeft;
    private double _visibleTop;
    private DockEdge _dockEdge = DockEdge.Right;
    private readonly DispatcherTimer _hideTimer;
    private UsagePopup? _popup;

    public MainWindow(StatusBallViewModel viewModel, SettingsService settingsService, AppSettings settings)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideToEdge();
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

        Root.ContextMenu = BuildContextMenu();
        Loaded += (_, _) =>
        {
            CaptureVisiblePosition();
            ScheduleHide();
        };
        MouseEnter += (_, _) => ShowFromEdge();
        MouseLeave += (_, _) => ScheduleHide();
    }

    protected override async void OnClosed(EventArgs e)
    {
        await SavePositionAsync();
        base.OnClosed(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ShowFromEdge();
        _hideTimer.Stop();
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
            SnapToNearestEdge();
            await SavePositionAsync();
            ScheduleHide();
        }
        else
        {
            TogglePopup();
        }

        base.OnMouseLeftButtonUp(e);
    }

    private ContextMenu BuildContextMenu()
    {
        var refresh = new MenuItem { Header = "Refresh" };
        refresh.Click += async (_, _) => await _viewModel.RefreshAsync();

        var alwaysOnTop = new MenuItem { Header = "Always On Top", IsCheckable = true, IsChecked = Topmost };
        alwaysOnTop.Click += async (_, _) =>
        {
            Topmost = alwaysOnTop.IsChecked;
            _settings.AlwaysOnTop = Topmost;
            await _settingsService.SaveAsync(_settings);
        };

        var edgeHide = new MenuItem { Header = "Edge Hide", IsCheckable = true, IsChecked = _settings.EdgeHideEnabled };
        edgeHide.Click += async (_, _) =>
        {
            _settings.EdgeHideEnabled = edgeHide.IsChecked;
            if (_settings.EdgeHideEnabled)
            {
                SnapToNearestEdge();
                ScheduleHide();
            }
            else
            {
                ShowFromEdge();
            }

            await SavePositionAsync();
        };

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        return new ContextMenu
        {
            Items =
            {
                refresh,
                alwaysOnTop,
                edgeHide,
                new Separator(),
                exit
            }
        };
    }

    private void TogglePopup()
    {
        ShowFromEdge();
        _hideTimer.Stop();

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
            Left = Left + Width + 8,
            Top = Top
        };
        _popup.Closed += (_, _) => _popup = null;
        _popup.Show();
    }

    private void PlaceAtDefaultTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Right - Width - 16;
        Top = workArea.Top + 24;
        _dockEdge = DockEdge.Right;
    }

    private void SnapToNearestEdge()
    {
        var workArea = SystemParameters.WorkArea;
        var distances = new Dictionary<DockEdge, double>
        {
            [DockEdge.Left] = Math.Abs(Left - workArea.Left),
            [DockEdge.Right] = Math.Abs(workArea.Right - (Left + Width)),
            [DockEdge.Top] = Math.Abs(Top - workArea.Top),
            [DockEdge.Bottom] = Math.Abs(workArea.Bottom - (Top + Height))
        };

        _dockEdge = distances.OrderBy(pair => pair.Value).First().Key;

        switch (_dockEdge)
        {
            case DockEdge.Left:
                Left = workArea.Left;
                Top = Clamp(Top, workArea.Top, workArea.Bottom - Height);
                break;
            case DockEdge.Right:
                Left = workArea.Right - Width;
                Top = Clamp(Top, workArea.Top, workArea.Bottom - Height);
                break;
            case DockEdge.Top:
                Top = workArea.Top;
                Left = Clamp(Left, workArea.Left, workArea.Right - Width);
                break;
            case DockEdge.Bottom:
                Top = workArea.Bottom - Height;
                Left = Clamp(Left, workArea.Left, workArea.Right - Width);
                break;
        }

        CaptureVisiblePosition();
    }

    private void CaptureVisiblePosition()
    {
        _visibleLeft = Left;
        _visibleTop = Top;
    }

    private void ShowFromEdge()
    {
        _hideTimer.Stop();
        if (!_isHiddenAtEdge)
        {
            return;
        }

        Left = _visibleLeft;
        Top = _visibleTop;
        _isHiddenAtEdge = false;
    }

    private void ScheduleHide()
    {
        if (!_settings.EdgeHideEnabled || _popup is { IsVisible: true } || IsMouseOver || _isDragging)
        {
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void HideToEdge()
    {
        if (!_settings.EdgeHideEnabled || IsMouseOver || _popup is { IsVisible: true })
        {
            return;
        }

        if (!_isHiddenAtEdge)
        {
            CaptureVisiblePosition();
        }

        const double visibleStrip = 6;
        var workArea = SystemParameters.WorkArea;

        switch (_dockEdge)
        {
            case DockEdge.Left:
                Left = workArea.Left - Width + visibleStrip;
                break;
            case DockEdge.Right:
                Left = workArea.Right - visibleStrip;
                break;
            case DockEdge.Top:
                Top = workArea.Top - Height + visibleStrip;
                break;
            case DockEdge.Bottom:
                Top = workArea.Bottom - visibleStrip;
                break;
        }

        _isHiddenAtEdge = true;
    }

    private static double Clamp(double value, double minimum, double maximum)
        => Math.Min(Math.Max(value, minimum), maximum);

    private async Task SavePositionAsync()
    {
        if (_isHiddenAtEdge)
        {
            Left = _visibleLeft;
            Top = _visibleTop;
            _isHiddenAtEdge = false;
        }

        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Opacity = Opacity;
        _settings.AlwaysOnTop = Topmost;
        await _settingsService.SaveAsync(_settings);
    }

    private enum DockEdge
    {
        Left,
        Right,
        Top,
        Bottom
    }
}
