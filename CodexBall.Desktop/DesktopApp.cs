using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using CodexBall.App.Services;

namespace CodexBall.Desktop;

internal sealed class DesktopApp : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Mutex? _mutex;
    private bool _ownsMutex, _exiting;
    private StatusBallViewModel? _viewModel;
    private WidgetWindow? _widget;
    private GlobalShortcuts? _shortcuts;
    private DesktopTray? _tray;
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop; desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, e) => { if (!_exiting) { e.Cancel = true; _ = ExitAsync(); } };
            Dispatcher.UIThread.Post(async () =>
            {
                try { await StartAsync(); }
                catch (Exception error) { Log(error); ShowMessage(error.ToString()); }
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
    private async Task StartAsync()
    {
        if (!Program.Smoke)
        {
            _mutex = new Mutex(true, OperatingSystem.IsWindows() ? "Global\\CodexBall" : "CodexBall.Desktop", out var created);
            if (!created) { _desktop!.Shutdown(); return; }
            _ownsMutex = true;
        }
        var store = new SettingsService(Program.Smoke ? Path.Combine(Path.GetTempPath(), "CodexBall-DesktopSmoke", Guid.NewGuid().ToString("N")) : null);
        var settings = await store.LoadAsync();
        if (Program.RestoreSmoke) { settings.SkinId = "local-koharu"; settings.CompanionMode = true; settings.EdgeHideEnabled = true; }
        AppLocalization.Current.Apply(settings.Language);
        _viewModel = new StatusBallViewModel(new CodexProcessMonitor());
        _widget = new WidgetWindow(_viewModel, store, settings);
        _desktop!.MainWindow = _widget;
        _widget.Show();
        _shortcuts = new GlobalShortcuts(_widget, _widget.ToggleEdgeHide, _widget.CycleAppearance);
        if (!_shortcuts.Succeeded)
            ShowMessage(AppLocalization.Format("Unable to register shortcuts.\n{0}", _shortcuts.Status));
        var updates = new UpdateService();
        _tray = new DesktopTray(_widget, _viewModel, updates, () => _ = ExitAsync());
        if (Program.Smoke) { await DesktopSmoke.RunAsync(_widget, _viewModel, store, _shortcuts.Status); await ExitAsync(DesktopSmoke.ExitCode); return; }
        _widget.ApplyVisibility();
        if (!DesktopPlatform.IsPackaged) _ = updates.CheckAsync();
        await _viewModel.StartAsync();
    }
    private async Task ExitAsync(int code = 0)
    {
        if (_exiting) return; _exiting = true;
        if (_viewModel is not null) await _viewModel.DisposeAsync();
        if (_widget is not null) { await _widget.SaveAsync(); _widget.Close(); }
        _tray?.Dispose(); _shortcuts?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex(); _mutex?.Dispose();
        _desktop?.Shutdown(code);
    }
    public static void Log(Exception error)
    {
        try
        {
            var dir = OperatingSystem.IsMacOS() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Logs/CodexBall") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexBall");
            Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "desktop.log"), $"{DateTimeOffset.Now:O} {error}\n");
        }
        catch (IOException) { }
    }
    public static void ShowMessage(string message)
    {
        if (Program.Smoke) { Log(new InvalidOperationException(message)); return; }
        Dispatcher.UIThread.Post(() =>
        {
            var content = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
            var window = new Window { Title = "CodexBall", Width = 580, SizeToContent = SizeToContent.Height, Content = content, Topmost = true };
            var close = new Button { Content = AppLocalization.Text("Close"), HorizontalAlignment = HorizontalAlignment.Right }; close.Click += (_, _) => window.Close(); content.Children.Add(close);
            window.Show();
        });
    }
}
