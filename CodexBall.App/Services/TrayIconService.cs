using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using CodexBall.App.ViewModels;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using WpfApplication = System.Windows.Application;
using WinForms = System.Windows.Forms;

namespace CodexBall.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly StatusBallViewModel _viewModel;
    private readonly Window _window;
    private readonly Icon _defaultIcon;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _statusItem;
    private bool _disposed;
    private Icon? _dynamicIcon;

    public TrayIconService(StatusBallViewModel viewModel, Window window)
    {
        _viewModel = viewModel;
        _window = window;
        _defaultIcon = LoadDefaultIcon();

        _statusItem = new WinForms.ToolStripMenuItem("Waiting for Codex") { Enabled = false };
        var refreshItem = new WinForms.ToolStripMenuItem("Refresh");
        refreshItem.Click += async (_, _) => await _viewModel.RefreshAsync();

        var showItem = new WinForms.ToolStripMenuItem("Show");
        showItem.Click += (_, _) =>
        {
            if (_viewModel.IsCodexActive)
            {
                _window.Show();
                _window.Activate();
            }
        };

        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => WpfApplication.Current.Shutdown();

        _notifyIcon = new WinForms.NotifyIcon
        {
            ContextMenuStrip = new WinForms.ContextMenuStrip(),
            Icon = _defaultIcon,
            Text = "CodexBall",
            Visible = true
        };
        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _statusItem,
            new WinForms.ToolStripSeparator(),
            refreshItem,
            showItem,
            new WinForms.ToolStripSeparator(),
            exitItem
        ]);
        _notifyIcon.DoubleClick += (_, _) =>
        {
            if (_viewModel.IsCodexActive)
            {
                _window.Show();
                _window.Activate();
            }
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CodexActivityChanged += OnCodexActivityChanged;
        UpdateIcon();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StatusBallViewModel.TrayUsagePercent)
            or nameof(StatusBallViewModel.TooltipText)
            or nameof(StatusBallViewModel.ShortWindowPercent))
        {
            UpdateIcon();
        }
    }

    private void OnCodexActivityChanged(object? sender, bool isActive)
        => UpdateIcon();

    private void UpdateIcon()
    {
        var percent = _viewModel.TrayUsagePercent;
        _statusItem.Text = percent is null ? "Waiting for Codex" : _viewModel.ShortWindowPercent;
        _notifyIcon.Text = TrimNotifyText(percent is null ? "CodexBall" : $"CodexBall {percent}%");

        if (percent is null)
        {
            SetIcon(_defaultIcon);
            return;
        }

        SetIcon(RenderUsageIcon(percent.Value));
    }

    private void SetIcon(Icon icon)
    {
        var previousDynamicIcon = _dynamicIcon;
        _dynamicIcon = ReferenceEquals(icon, _defaultIcon) ? null : icon;
        _notifyIcon.Icon = icon;

        if (previousDynamicIcon is not null && !ReferenceEquals(previousDynamicIcon, icon))
        {
            previousDynamicIcon.Dispose();
        }
    }

    private static Icon RenderUsageIcon(int percent)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var ringColor = percent switch
        {
            >= 50 => Color.FromArgb(57, 217, 138),
            >= 20 => Color.FromArgb(245, 187, 64),
            _ => Color.FromArgb(246, 91, 91)
        };

        using var backgroundBrush = new SolidBrush(Color.FromArgb(235, 22, 26, 32));
        using var ringPen = new Pen(ringColor, 3.2f);
        using var textBrush = new SolidBrush(Color.White);
        using var font = new DrawingFont("Segoe UI", percent >= 100 ? 10.5f : 13.5f, DrawingFontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        graphics.FillEllipse(backgroundBrush, 1, 1, size - 2, size - 2);
        graphics.DrawEllipse(ringPen, 2.5f, 2.5f, size - 5, size - 5);
        graphics.DrawString(percent.ToString(), font, textBrush, new RectangleF(0, 0, size, size), format);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Icon LoadDefaultIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/CodexBall.ico"));
        if (resource is null)
        {
            return (Icon)SystemIcons.Application.Clone();
        }

        using var stream = resource.Stream;
        return new Icon(stream);
    }

    private static string TrimNotifyText(string text)
        => text.Length <= 63 ? text : text[..63];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CodexActivityChanged -= OnCodexActivityChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _dynamicIcon?.Dispose();
        _defaultIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
