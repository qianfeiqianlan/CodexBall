using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace CodexBall.AvaloniaProbe;

internal sealed class ProbeApp : Application
{
    private GlobalShortcuts? _shortcuts;
    private TrayIcon? _tray;
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var widget = new WidgetWindow();
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var panel = new StackPanel { Margin = new Thickness(24), Spacing = 14 };
            panel.Children.Add(new TextBlock { Text = AppLocalization.Title, FontSize = 22 });
            panel.Children.Add(new TextBlock { Text = AppLocalization.Text("透明悬浮窗 / Live2D 最小验证\n用量条使用演示数据，不连接 Codex，不修改正式设置。\n左键拖动；点击小球切换双环；点击角色播放动作。", "Transparent widget / Live2D minimal probe\nDemo usage only. No Codex connection or production settings changes.\nDrag to move; click ring to toggle dual rings; click character to play motion."), TextWrapping = TextWrapping.Wrap });
            var controls = new Window { Title = AppLocalization.Title, Width = 580, Height = 520, Content = panel };
            void Button(string zh, string en, Action action)
            {
                var button = new Button { Content = AppLocalization.Text(zh, en), HorizontalAlignment = HorizontalAlignment.Stretch };
                button.Click += (_, _) => action(); panel.Children.Add(button);
            }
            widget.Report = message => status.Text = message;
            if (WidgetWindow.KoharuAvailable) Button("切换 CodexBall / 小春（Alt+C）", "Switch CodexBall / Koharu (Alt+C)", widget.ToggleAppearance);
            Button("贴边隐藏 / 恢复（Alt+H）", "Edge hide / restore (Alt+H)", widget.ToggleEdgeHide);
            Button("切换置顶", "Toggle always on top", () => widget.Topmost = !widget.Topmost);
            Button("退出验证程序", "Exit probe", () => desktop.Shutdown());
            panel.Children.Add(status);
            desktop.MainWindow = controls;
            widget.Show();
            _shortcuts = new GlobalShortcuts(widget, widget.ToggleEdgeHide, widget.ToggleAppearance);
            status.Text = _shortcuts.Status;
            var menu = new NativeMenu();
            void Item(string zh, string en, Action action)
            {
                var item = new NativeMenuItem(AppLocalization.Text(zh, en)); item.Click += (_, _) => action(); menu.Items.Add(item);
            }
            Item("显示验证面板", "Show probe panel", () => { controls.Show(); controls.Activate(); });
            var appearances = new NativeMenu();
            var ringItem = new NativeMenuItem("CodexBall"); ringItem.Click += (_, _) => widget.ShowRing(); appearances.Items.Add(ringItem);
            if (WidgetWindow.KoharuAvailable)
            {
                var koharuItem = new NativeMenuItem(AppLocalization.Text("小春 Koharu", "Koharu"));
                koharuItem.Click += (_, _) => { if (!widget.IsCompanion) widget.ToggleAppearance(); }; appearances.Items.Add(koharuItem);
            }
            menu.Items.Add(new NativeMenuItem(AppLocalization.Text("外观", "Appearance")) { Menu = appearances });
            Item("贴边隐藏 / 恢复", "Edge hide / restore", widget.ToggleEdgeHide);
            Item("退出", "Exit", () => desktop.Shutdown());
            _tray = new TrayIcon { Icon = new WindowIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "probe.ico")), ToolTipText = AppLocalization.Title, Menu = menu, IsVisible = true };
            TrayIcon.SetIcons(this, [_tray]);
            controls.Closing += (_, e) => { if (!Program.Arguments.Contains("--smoke")) { e.Cancel = true; controls.Hide(); } };
            desktop.Exit += (_, _) => { _shortcuts.Dispose(); _tray.Dispose(); widget.Close(); };
            if (Program.Arguments.Contains("--smoke"))
            {
                controls.Show();
                Dispatcher.UIThread.Post(async () => await SmokeAsync(widget, desktop));
            }
            else controls.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task SmokeAsync(WidgetWindow widget, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var output = System.IO.Path.GetFullPath("artifacts/avalonia-probe");
        System.IO.Directory.CreateDirectory(output);
        var checks = new Dictionary<string, object?> { ["os"] = RuntimeInformation.OSDescription, ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(), ["shortcuts"] = _shortcuts?.Status };
        var exitCode = 0;
        try
        {
            await Task.Delay(400);
            checks["coreProfile"] = Program.UseCoreProfile;
            checks["transparency"] = widget.ActualTransparencyLevel.ToString();
            checks["ringSize"] = $"{widget.Width}x{widget.Height}";
            var origin = widget.Position;
            widget.ToggleEdgeHide();
            checks["ringEdgeHidden"] = widget.IsEdgeHidden && widget.Position != origin;
            widget.Reveal();
            checks["ringPositionRestored"] = widget.Position == origin;
            checks["koharuAvailable"] = WidgetWindow.KoharuAvailable;
            checks["applicationDirectory"] = AppContext.BaseDirectory;
            widget.ToggleAppearance();
            var player = widget.Player ?? throw new InvalidOperationException("Live2D could not be loaded: " + widget.LastError);
            await Task.Delay(700);
            if (widget.Player is null) throw new InvalidOperationException("Live2D frame update failed: " + widget.LastError);
            checks["renderer"] = player.RendererDescription;
            checks["frames"] = player.RenderedFrames;
            byte[] CopyPixels()
            {
                using var locked = player.Image.Lock();
                var bytes = new byte[locked.RowBytes * locked.Size.Height];
                Marshal.Copy(locked.Address, bytes, 0, bytes.Length); return bytes;
            }
            player.Image.Save(System.IO.Path.Combine(output, "live2d-frame.png"), new PngBitmapEncoderOptions());
            checks["widgetBounds"] = widget.Bounds.ToString();
            checks["scale"] = widget.RenderScaling;
            var initial = CopyPixels();
            var alpha = initial.Where((_, i) => i % 4 == 3).ToArray();
            checks["transparentPixels"] = alpha.Count(a => a == 0);
            checks["visiblePixels"] = alpha.Count(a => a > 0);
            player.Play(CodexBall.Core.Companion.CompanionAction.Pat);
            player.SetLookTarget(.8, .5);
            await Task.Delay(400);
            checks["animatedPixels"] = !initial.SequenceEqual(CopyPixels());
            using (var screenshot = new RenderTargetBitmap(new PixelSize(480, 560), new Vector(192, 192)))
            { screenshot.Render((Visual)widget.Content!); screenshot.Save(System.IO.Path.Combine(output, "koharu.png"), new PngBitmapEncoderOptions()); }
            using (var composed = SkiaSharp.SKBitmap.Decode(System.IO.Path.Combine(output, "koharu.png")))
            {
                var ratio = composed.Pixels.Count(pixel => pixel.Alpha > 0) / (double)alpha.Count(a => a > 0);
                checks["composedPixelRatio"] = ratio;
                checks["composedScaleCorrect"] = ratio is > .85 and < 1.2;
            }
            var beforeHide = widget.Position;
            widget.ToggleEdgeHide(); var frames = player.RenderedFrames;
            await Task.Delay(180);
            checks["hiddenPaused"] = frames == player.RenderedFrames;
            widget.Reveal();
            checks["companionPositionRestored"] = widget.Position == beforeHide;
            await Task.Delay(180);
            checks["resumed"] = player.RenderedFrames > frames;
            checks["lastRenderMilliseconds"] = player.LastFrameMilliseconds;
            widget.ToggleAppearance();
            var disposed = false;
            try { player.RenderFrame(.016f); } catch (ObjectDisposedException) { disposed = true; }
            checks["releasedOnRing"] = disposed && widget.Player is null;
            for (var i = 0; i < 3; i++)
            {
                widget.ToggleAppearance();
                if (widget.Player is null) throw new InvalidOperationException("Repeated model loading failed: " + widget.LastError);
                await Task.Delay(80); widget.ToggleAppearance();
            }
            checks["reloadCycles"] = 3;
            if (checks.Where(pair => pair.Key != "coreProfile").Select(pair => pair.Value).OfType<bool>().Any(value => !value) || alpha.All(a => a == 0) || alpha.All(a => a > 0) || widget.ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
                throw new InvalidOperationException("A required smoke check failed.");
            checks["result"] = "passed";
        }
        catch (Exception error) { checks["result"] = "failed"; checks["error"] = error.ToString(); exitCode = 1; }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, "smoke.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(exitCode);
        }
    }
}
