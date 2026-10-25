using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CodexBall.App.Companion;
using CodexBall.App.Services;
using SkiaSharp;

namespace CodexBall.Desktop;

internal sealed class DesktopTray : IDisposable
{
    private readonly WidgetWindow _window;
    private readonly StatusBallViewModel _viewModel;
    private readonly UpdateService _updates;
    private readonly Action _exit;
    private readonly TrayIcon _tray;
    private readonly WindowIcon _default = new(Path.Combine(AppContext.BaseDirectory, "CodexBall.ico"));
    public NativeMenu Menu => _tray.Menu!;
    public DesktopTray(WidgetWindow window, StatusBallViewModel viewModel, UpdateService updates, Action exit)
    {
        _window = window; _viewModel = viewModel; _updates = updates; _exit = exit;
        _tray = new TrayIcon { Icon = _default, IsVisible = true, ToolTipText = "CodexBall" };
        TrayIcon.SetIcons(Application.Current!, [_tray]);
        window.StateChanged += Rebuild;
        viewModel.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(viewModel.TrayUsagePercent) or nameof(viewModel.TooltipText)) { UpdateIcon(); Rebuild(); } };
        updates.StateChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild(); UpdateIcon();
    }
    private static NativeMenuItem Item(string label, Action action, bool? check = null)
    {
        var item = new NativeMenuItem(label);
        if (check.HasValue) { item.ToggleType = MenuItemToggleType.CheckBox; item.IsChecked = check.Value; }
        item.Click += (_, _) => { try { action(); } catch (Exception e) { DesktopApp.ShowMessage(e.Message); } }; return item;
    }
    private string Shortcut(string label) => OperatingSystem.IsMacOS() ? AppLocalization.Text(label).Replace("Alt+", "Option+") : AppLocalization.Text(label);
    public void Rebuild()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem(_viewModel.TrayUsagePercent is null ? AppLocalization.Text("Waiting for Codex") : _viewModel.ShortWindowPercent) { IsEnabled = false });
        menu.Items.Add(Item(AppLocalization.Text("Codex usage"), _window.ShowDetails));
        menu.Items.Add(Item(AppLocalization.Text("Refresh"), () => _ = _viewModel.RefreshAsync()));
        var appearances = new NativeMenu();
        foreach (var entry in SkinCatalog.Discover(out _))
        {
            var name = AppLocalization.Current.IsChinese ? entry.Name : entry.Id switch { "local-koharu" => "Koharu", "local-haruto" => "Haruto", "local-mao_pro" => "Mao", "local-mark_free_t04" => "Mark", _ => entry.Name };
            appearances.Items.Add(Item(name, () => _window.SelectAppearance(entry.Id), _window.SelectedSkinId == entry.Id));
        }
        menu.Items.Add(new NativeMenuItem(Shortcut("Appearance (Alt+C)")) { Menu = appearances });
        var languages = new NativeMenu();
        foreach (var (id, label) in new[] { ("system", AppLocalization.Text("Follow system")), ("zh-CN", "中文"), ("en-US", "English") })
            languages.Items.Add(Item(label, () => _window.SetLanguage(id), AppLocalization.Current.Preference == id));
        menu.Items.Add(new NativeMenuItem(AppLocalization.Text("Language")) { Menu = languages });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(Shortcut("Edge Hide (Alt+H)"), _window.ToggleEdgeHide, _window.Settings.EdgeHideEnabled));
        menu.Items.Add(Item(AppLocalization.Text("Always On Top"), () => _window.SetTopmost(!_window.Settings.AlwaysOnTop), _window.Settings.AlwaysOnTop));
        if (!DesktopPlatform.IsPackaged)
        {
            menu.Items.Add(Item(AppLocalization.Text("Launch at Startup"), () => { DesktopPlatform.SetStartup(!DesktopPlatform.StartupEnabled()); Rebuild(); }, DesktopPlatform.StartupEnabled()));
            if (_updates.IsUpgradeAvailable)
                menu.Items.Add(Item(AppLocalization.Format("Upgrade to {0}", _updates.LatestVersion!), () => _ = UpgradeAsync()));
        }
        menu.Items.Add(Item(AppLocalization.Text("Home"), DesktopPlatform.OpenHome));
        menu.Items.Add(new NativeMenuItem(AppLocalization.Format("Version {0}", UpdateService.CurrentVersion)) { IsEnabled = false });
        menu.Items.Add(Item(AppLocalization.Text("Exit"), _exit)); _tray.Menu = menu;
    }
    private async Task UpgradeAsync()
    {
        try { if (await _updates.StartUpgradeAsync()) _exit(); }
        catch (Exception e) { DesktopApp.ShowMessage(e.Message); }
    }
    private void UpdateIcon()
    {
        var percent = _viewModel.TrayUsagePercent;
        _tray.ToolTipText = percent is null ? "CodexBall" : $"CodexBall {percent}%";
        if (percent is null) { _tray.Icon = _default; return; }
        using var bitmap = new SKBitmap(32, 32); using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(22, 26, 32, 235) }; canvas.DrawCircle(16, 16, 15, paint);
        paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 2;
        paint.Color = percent >= 50 ? new SKColor(57, 217, 138) : percent >= 20 ? new SKColor(245, 187, 64) : new SKColor(246, 91, 91); canvas.DrawCircle(16, 16, 14, paint);
        paint.Style = SKPaintStyle.Fill; paint.Color = SKColors.White;
        using var font = new SKFont(SKTypeface.Default, percent >= 100 ? 14 : 18);
        var text = percent.ToString(); var width = font.MeasureText(text);
        canvas.DrawText(text, (32 - width) / 2, 16 - (font.Metrics.Ascent + font.Metrics.Descent) / 2, font, paint);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = data.AsStream();
        _tray.Icon = new WindowIcon(stream);
    }
    public void Dispose() { _window.StateChanged -= Rebuild; _tray.Dispose(); }
}
