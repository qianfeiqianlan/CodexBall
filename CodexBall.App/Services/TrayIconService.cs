using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using CodexBall.App.ViewModels;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using WpfApplication = System.Windows.Application;
using WinForms = System.Windows.Forms;

namespace CodexBall.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly StatusBallViewModel _viewModel;
    private readonly MainWindow _window;
    private readonly UpdateService _updateService;
    private readonly StartupService _startupService = new();
    private readonly bool _isPackaged = AppPackageService.IsPackaged();
    private readonly Icon _defaultIcon;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _statusItem;
    private readonly WinForms.ToolStripMenuItem _alwaysOnTopItem;
    private readonly WinForms.ToolStripMenuItem _edgeHideItem;
    private readonly WinForms.ToolStripMenuItem _launchAtStartupItem;
    private readonly WinForms.ToolStripMenuItem _upgradeItem;
    private readonly WinForms.ToolStripMenuItem _versionItem;
    private readonly WinForms.ToolStripMenuItem _skinsItem;
    private readonly WinForms.ToolStripMenuItem _languageItem;
    private readonly WinForms.ToolStripMenuItem _refreshItem;
    private readonly WinForms.ToolStripMenuItem _homeItem;
    private readonly WinForms.ToolStripMenuItem _exitItem;
    private bool _disposed;
    private Icon? _dynamicIcon;

    public TrayIconService(StatusBallViewModel viewModel, MainWindow window, UpdateService updateService)
    {
        _viewModel = viewModel;
        _window = window;
        _updateService = updateService;
        _defaultIcon = LoadDefaultIcon();

        _statusItem = new WinForms.ToolStripMenuItem("Waiting for Codex") { Enabled = false };
        _refreshItem = new WinForms.ToolStripMenuItem("Refresh");
        _refreshItem.Click += async (_, _) => await _viewModel.RefreshAsync();

        _alwaysOnTopItem = new WinForms.ToolStripMenuItem("Always On Top") { CheckOnClick = true };
        _alwaysOnTopItem.Click += async (_, _) => await _window.SetAlwaysOnTopAsync(_alwaysOnTopItem.Checked);

        _edgeHideItem = new WinForms.ToolStripMenuItem("Edge Hide (Alt+H)") { CheckOnClick = true };
        _edgeHideItem.Click += async (_, _) => await _window.SetEdgeHideModeAsync(_edgeHideItem.Checked);

        _languageItem = new WinForms.ToolStripMenuItem();
        _languageItem.DropDownOpening += (_, _) => PopulateLanguages();
        _skinsItem = new WinForms.ToolStripMenuItem("外观 (Alt+C)");
        _skinsItem.DropDownOpening += (_, _) => PopulateSkins();

        _launchAtStartupItem = new WinForms.ToolStripMenuItem("Launch at Startup") { CheckOnClick = true };
        _launchAtStartupItem.Visible = !_isPackaged;
        _launchAtStartupItem.Click += (_, _) =>
        {
            _startupService.SetEnabled(_launchAtStartupItem.Checked);
            _launchAtStartupItem.Checked = _startupService.IsEnabled();
        };

        _homeItem = new WinForms.ToolStripMenuItem("Home");
        _homeItem.Click += (_, _) => ProjectHomeService.Open();

        _upgradeItem = new WinForms.ToolStripMenuItem("Upgrade") { Visible = false };
        _upgradeItem.Click += async (_, _) => await StartUpgradeAsync();

        _versionItem = new WinForms.ToolStripMenuItem($"Version {_updateService.CurrentVersionText}") { Enabled = false };

        _exitItem = new WinForms.ToolStripMenuItem("Exit");
        _exitItem.Click += (_, _) => WpfApplication.Current.Shutdown();

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
            _refreshItem,
            _skinsItem,
            _languageItem,
            new WinForms.ToolStripSeparator(),
            _edgeHideItem,
            _alwaysOnTopItem,
            new WinForms.ToolStripSeparator(),
            _launchAtStartupItem,
            _homeItem,
            _upgradeItem,
            new WinForms.ToolStripSeparator(),
            _versionItem,
            new WinForms.ToolStripSeparator(),
            _exitItem
        ]);
        _notifyIcon.ContextMenuStrip.Opening += (_, _) => UpdateSettingsState();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CodexActivityChanged += OnCodexActivityChanged;
        _updateService.StateChanged += OnUpdateStateChanged;
        AppLocalization.Current.PropertyChanged += OnLanguageChanged;
        UpdateLanguage();
        UpdateSettingsState();
        UpdateIcon();
        UpdateUpgradeState();
    }

    private async Task StartUpgradeAsync()
    {
        try
        {
            await _updateService.StartUpgradeAsync();
            WpfApplication.Current.Shutdown();
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show(ex.Message, AppLocalization.Text("CodexBall Upgrade"), WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    private void OnUpdateStateChanged(object? sender, EventArgs e)
        => WpfApplication.Current.Dispatcher.Invoke(UpdateUpgradeState);

    private void UpdateUpgradeState()
    {
        _upgradeItem.Visible = !_isPackaged && _updateService.IsUpgradeAvailable;
        _upgradeItem.Enabled = !_updateService.IsUpgradeInProgress;
        _upgradeItem.Text = _updateService.LatestVersionText is null
            ? AppLocalization.Text("Upgrade")
            : AppLocalization.Format("Upgrade to {0}", _updateService.LatestVersionText);
        _versionItem.Text = AppLocalization.Format("Version {0}", _updateService.CurrentVersionText);
    }

    private void UpdateSettingsState()
    {
        _alwaysOnTopItem.Checked = _window.IsAlwaysOnTopEnabled;
        _edgeHideItem.Checked = _window.IsEdgeHideEnabled;
        if (!_isPackaged)
        {
            _launchAtStartupItem.Checked = _startupService.IsEnabled();
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => UpdateLanguage();

    private void UpdateLanguage()
    {
        _refreshItem.Text = AppLocalization.Text("Refresh");
        _homeItem.Text = AppLocalization.Text("Home");
        _exitItem.Text = AppLocalization.Text("Exit");
        _alwaysOnTopItem.Text = AppLocalization.Text("Always On Top");
        _edgeHideItem.Text = AppLocalization.Text("Edge Hide (Alt+H)");
        _skinsItem.Text = AppLocalization.Text("Appearance (Alt+C)");
        _languageItem.Text = AppLocalization.Text("Language");
        _launchAtStartupItem.Text = AppLocalization.Text("Launch at Startup");
        UpdateUpgradeState();
        UpdateIcon();
    }

    private void PopulateLanguages()
    {
        foreach (WinForms.ToolStripItem old in _languageItem.DropDownItems.Cast<WinForms.ToolStripItem>().ToArray()) old.Dispose();
        _languageItem.DropDownItems.Clear();
        foreach (var (id, label) in new[] { ("system", AppLocalization.Text("Follow system")), ("zh-CN", "中文"), ("en-US", "English") })
        {
            var item = new WinForms.ToolStripMenuItem(label) { Checked = id == AppLocalization.Current.Preference };
            item.Click += async (_, _) => await _window.SetLanguageAsync(id);
            _languageItem.DropDownItems.Add(item);
        }
    }

    private void PopulateSkins()
    {
        foreach (WinForms.ToolStripItem old in _skinsItem.DropDownItems.Cast<WinForms.ToolStripItem>().ToArray()) old.Dispose();
        _skinsItem.DropDownItems.Clear();
        foreach (var entry in Companion.SkinCatalog.Discover(out _))
        {
            var item = new WinForms.ToolStripMenuItem(AppLocalization.Current.IsChinese ? entry.Name : entry.Id switch { "local-koharu" => "Koharu", "local-haruto" => "Haruto", "local-mao_pro" => "Mao", "local-mark_free_t04" => "Mark", _ => entry.Name }) { Checked = entry.Id == _window.SelectedSkinId };
            item.Click += async (_, _) => await _window.SelectAppearanceAsync(entry.Id);
            _skinsItem.DropDownItems.Add(item);
        }
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
        _statusItem.Text = percent is null ? AppLocalization.Text("Waiting for Codex") : _viewModel.ShortWindowPercent;
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
        AppLocalization.Current.PropertyChanged -= OnLanguageChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CodexActivityChanged -= OnCodexActivityChanged;
        _updateService.StateChanged -= OnUpdateStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _dynamicIcon?.Dispose();
        _defaultIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
