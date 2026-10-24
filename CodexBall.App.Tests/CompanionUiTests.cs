using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexBall.App.Companion;
using CodexBall.App.Companion.Live2D;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;
using CodexBall.Core.Companion;

namespace CodexBall.App.Tests;

public sealed class CompanionUiTests
{
    [Theory]
    [InlineData("system", "zh-TW", "zh-CN")]
    [InlineData("system", "en-US", "en-US")]
    [InlineData("system", "ja-JP", "en-US")]
    [InlineData("en-US", "zh-CN", "en-US")]
    [InlineData("zh-CN", "en-US", "zh-CN")]
    [InlineData("invalid", "zh-CN", "zh-CN")]
    public void LanguageFollowsSystemUnlessOverridden(string preference, string system, string expected)
        => Assert.Equal(expected, AppLocalization.ResolveLanguage(preference, new System.Globalization.CultureInfo(system)));

    [Fact]
    public void LanguageUpdatesBindingsAndPersists()
        => RunSta(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CodexBall-language-" + Guid.NewGuid());
            var localization = AppLocalization.Current;
            var original = localization.Preference;
            var settingsService = new SettingsService(directory);
            await using var vm = new StatusBallViewModel(new CodexProcessMonitor());
            MainWindow? window = null;
            try
            {
                var settings = await settingsService.LoadAsync();
                Assert.Equal("system", settings.Language);
                window = new MainWindow(vm, settingsService, settings);
                var notifications = 0;
                vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.StatusText)) notifications++; };
                await window.SetLanguageAsync("zh-CN");
                Assert.Equal("暂不可用", vm.StatusText);
                Assert.Equal("加载中", vm.LastUpdatedText);
                Assert.Equal("zh-CN", (await settingsService.LoadAsync()).Language);
                await window.SetLanguageAsync("en-US");
                Assert.Equal("Unavailable", vm.StatusText);
                Assert.Equal("Loading", vm.LastUpdatedText);
                Assert.Equal("en-US", (await settingsService.LoadAsync()).Language);
                Assert.True(notifications >= 2);
            }
            finally
            {
                window?.Close();
                localization.Apply(original);
                // Closing saves asynchronously; queue a final save to await its completion.
                await settingsService.SaveAsync(new AppSettings());
                Directory.Delete(directory, true);
            }
        });

    [Fact]
    public void RejectsCropOutsideDecodedImage()
        => RunSta(() =>
        {
            var skin = SkinLoader.Load(SkinCatalog.LegacyDirectory);
            skin.Definition.Actions["Idle"].Frames[0].Crop!.Height = 1000;
            Assert.Throws<InvalidDataException>(() => new PngCompanionPlayer(skin));
        });

    [Fact]
    public void BuiltInSpritesDecodeAndPlayerRestoresIdleAfterPat()
        => RunSta(() =>
        {
            using var player = new PngCompanionPlayer(SkinLoader.Load(SkinCatalog.LegacyDirectory));
            var idle = Assert.IsAssignableFrom<BitmapSource>(player.Image);
            Assert.Equal(583, idle.PixelWidth);
            Assert.Equal(899, idle.PixelHeight);
            var completed = 0;
            player.Completed += (_, _) => { completed++; player.Play(CompanionAction.Idle); };
            player.Play(CompanionAction.Pat);
            Assert.NotSame(idle, player.Image);
            player.Pause();
            Pump(TimeSpan.FromMilliseconds(1300));
            Assert.Equal(0, completed);
            player.Resume();
            Pump(TimeSpan.FromMilliseconds(1400));
            Assert.Equal(1, completed);
            Assert.Same(idle, player.Image);
            player.Play(CompanionAction.Drag);
            var drag = player.Image;
            Pump(TimeSpan.FromMilliseconds(100));
            Assert.Same(drag, player.Image);
            player.Play(CompanionAction.Drop);
            Assert.Same(idle, player.Image);
        });

    [Fact]
    [Trait("Category", "Live2DGraphics")]
    public void AppearanceSelectionControlsModeAndPersistsWithoutWidgetMenu()
        => RunSta(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CodexBall-ui-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var service = new SettingsService(directory);
                var settingsPath = Path.Combine(directory, "settings.json");
                File.WriteAllText(settingsPath, """{"CompanionMode":true,"FocusMode":true,"SkinId":"live2d-haru"}""");
                var settings = await service.LoadAsync();
                var viewModel = new StatusBallViewModel(new CodexProcessMonitor());
                var window = new MainWindow(viewModel, service, settings);
                Assert.Equal(SkinCatalog.BallId, window.SelectedSkinId);
                Assert.False(window.IsCompanionMode);
                Assert.Equal(64, window.Width);
                Assert.Null(((FrameworkElement)window.Content).ContextMenu);
                Assert.DoesNotContain(SkinCatalog.Discover(out _), e => e.Id == "live2d-haru");
                await window.SelectAppearanceAsync("local-koharu");
                Assert.True(window.IsCompanionMode);
                Assert.Equal("local-koharu", window.SelectedSkinId);
                Assert.Equal(240, window.Width);
                Assert.Equal(280, window.Height);
                Assert.False(window.ShowInTaskbar);
                Assert.True(window.AllowsTransparency);
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(window.Width, window.Height));
                content.Arrange(new Rect(0, 0, window.Width, window.Height));
                content.UpdateLayout();
                var render = new RenderTargetBitmap(480, 560, 192, 192, PixelFormats.Pbgra32);
                render.Render(content);
                if (Environment.GetEnvironmentVariable("CODEXBALL_PREVIEW_PATH") is { } preview)
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
                    using var file = File.Create(preview); encoder.Save(file);
                }
                var bytes = new byte[480 * 560 * 4]; render.CopyPixels(bytes, 480 * 4, 0);
                Assert.Equal(0, bytes[3]);
                Assert.Contains(Enumerable.Range(0, bytes.Length / 4), i => bytes[i * 4 + 3] > 200);
                await window.SelectAppearanceAsync(SkinCatalog.BallId);
                Assert.False(window.IsCompanionMode);
                Assert.Equal(64, window.Width);
                Assert.Equal(64, window.Height);
                Assert.Equal(SkinCatalog.BallId, window.SelectedSkinId);
                var restored = await service.LoadAsync();
                Assert.False(restored.CompanionMode);
                Assert.Equal(SkinCatalog.BallId, restored.SkinId);
                Assert.DoesNotContain("FocusMode", File.ReadAllText(settingsPath));
                var appearances = SkinCatalog.Discover(out _);
                for (var i = 1; i <= appearances.Count; i++)
                {
                    await window.CycleAppearanceAsync();
                    var expected = appearances[i % appearances.Count].Id;
                    Assert.Equal(expected, window.SelectedSkinId);
                    Assert.Equal(expected != SkinCatalog.BallId, window.IsCompanionMode);
                }
                window.Close();
                await viewModel.DisposeAsync();
                Pump(TimeSpan.FromMilliseconds(50));
            }
            finally { Directory.Delete(directory, recursive: true); }
        });
    [Fact]
    [Trait("Category", "Live2DGraphics")]
    public void Live2DActuallyRendersAnimatedTransparentFramesAndRespondsToInput()
        => RunSta(() =>
        {
            using var player = new Live2DCompanionPlayer(SkinLoader.Load(SkinCatalog.BuiltInDirectory));
            var image = Assert.IsAssignableFrom<BitmapSource>(player.Image);
            var stride = image.PixelWidth * 4;
            byte[] Pixels() { var pixels = new byte[stride * image.PixelHeight]; image.CopyPixels(pixels, stride, 0); return pixels; }
            var initial = Pixels();
            Assert.Equal(0, initial[3]);
            Assert.Contains(Enumerable.Range(0, initial.Length / 4), i => initial[i * 4 + 3] > 200);
            var frameTimes = new List<double>();
            for (var i = 0; i < 120; i++) { player.RenderFrame(1f / 60); frameTimes.Add(player.LastFrameMilliseconds); }
            Assert.False(initial.SequenceEqual(Pixels()));
            player.SetLookTarget(1, .5);
            player.Play(CompanionAction.Pat);
            for (var i = 0; i < 60; i++) player.RenderFrame(1f / 60);
            var reaction = Pixels();
            player.Play(CompanionAction.Drag);
            player.SetDragImpulse(-10);
            for (var i = 0; i < 30; i++) player.RenderFrame(1f / 60);
            Assert.False(reaction.SequenceEqual(Pixels()));
            player.Play(CompanionAction.Drop);
            for (var i = 0; i < 60; i++) player.RenderFrame(1f / 60);
            if (Environment.GetEnvironmentVariable("CODEXBALL_LIVE2D_REPORT") is { } report)
                File.WriteAllText(report, $"Renderer: {player.RendererDescription}\nResolution: {image.PixelWidth}x{image.PixelHeight}\nFrames: {player.RenderedFrames}\nMean render+readback ms: {frameTimes.Average():F2}\nP95 render+readback ms: {frameTimes.Order().ElementAt((int)(frameTimes.Count * .95)):F2}\n");
        });

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    [Fact]
    [Trait("Category", "Live2DGraphics")]
    public void ImportedModelsRenderWhenInstalled()
        => RunSta(() =>
        {
            foreach (var entry in SkinCatalog.Discover(out _).Where(e => e.Directory is not null && e.Id.StartsWith("local-", StringComparison.Ordinal)))
            {
                var skin = SkinLoader.Load(entry.Directory!);
                using var player = new Live2DCompanionPlayer(skin);
                var image = Assert.IsAssignableFrom<BitmapSource>(player.Image);
                var stride = image.PixelWidth * 4;
                var before = new byte[stride * image.PixelHeight]; image.CopyPixels(before, stride, 0);
                Assert.Contains(Enumerable.Range(0, before.Length / 4), i => before[i * 4 + 3] > 200);
                player.Play(CompanionAction.Pat);
                player.SetLookTarget(.8, .3);
                for (var i = 0; i < 45; i++) player.RenderFrame(1f / 60);
                var after = new byte[before.Length]; image.CopyPixels(after, stride, 0);
                Assert.False(before.SequenceEqual(after), $"{skin.Definition.Name} did not animate.");
                if (Environment.GetEnvironmentVariable("CODEXBALL_IMPORTED_PREVIEW_DIR") is { } previewDirectory)
                {
                    Directory.CreateDirectory(previewDirectory);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using var file = File.Create(Path.Combine(previewDirectory, skin.Definition.Id + ".png")); encoder.Save(file);
                }
            }
        });

    private static void RunSta(Action action) => RunSta(() => { action(); return Task.CompletedTask; });

    private static void RunSta(Func<Task> action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await action(); }
                catch (Exception ex) { error = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
