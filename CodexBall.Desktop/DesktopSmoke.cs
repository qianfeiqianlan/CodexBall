using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CodexBall.App.Companion;
using CodexBall.App.Services;
using CodexBall.Core.Models;

namespace CodexBall.Desktop;

internal static class DesktopSmoke
{
    public static int ExitCode { get; private set; }
    public static async Task RunAsync(WidgetWindow widget, StatusBallViewModel vm, SettingsService store, string shortcuts)
    {
        var output = Path.GetFullPath("artifacts/desktop-smoke"); Directory.CreateDirectory(output);
        var results = new Dictionary<string, object?> { ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription, ["shortcuts"] = shortcuts };
        try
        {
            await Task.Delay(200);
            if (Program.RestoreSmoke)
            {
                var restoredPlayer = widget.Player ?? throw new InvalidOperationException(widget.LastError);
                results["startupSelection"] = widget.SelectedSkinId == "local-koharu";
                results["startupEdgeHidden"] = widget.IsEdgeHidden;
                var before = restoredPlayer.RenderedFrames; await Task.Delay(150);
                results["startupHiddenPaused"] = restoredPlayer.RenderedFrames == before;
                widget.SetEdgeHide(false); await Task.Delay(200);
                results["startupRevealResumed"] = restoredPlayer.RenderedFrames > before;
            }
            vm.ApplySnapshot(new CodexUsageSnapshot(AccountState.Available, new RateLimitWindow(25, 300, DateTimeOffset.UtcNow.AddHours(2)), new RateLimitWindow(50, 10080, DateTimeOffset.UtcNow.AddDays(2)), DateTimeOffset.Now), true);
            results["realViewModel"] = vm.CenterText == "75";
            results["transparent"] = widget.ActualTransparencyLevel == WindowTransparencyLevel.Transparent;
            var entries = SkinCatalog.Discover(out var warning); results["catalogWarning"] = warning;
            results["catalog"] = entries.Select(e => e.Id).ToArray();
            async Task CheckDragEdges(string id)
            {
                widget.SetEdgeHide(false);
                var area = (widget.Screens.ScreenFromWindow(widget) ?? widget.Screens.Primary)!.WorkingArea;
                var width = (int)(widget.Width * widget.RenderScaling);
                var height = (int)(widget.Height * widget.RenderScaling);
                var x = area.X + (area.Width-width)/2;
                var y = area.Y + (area.Height-height)/2;
                foreach(var position in new[] { new PixelPoint(area.X+2,y),new PixelPoint(area.Right-width-2,y),new PixelPoint(x,area.Y+2),new PixelPoint(x,area.Bottom-height-2) })
                {
                    widget.SetEdgeHide(false); widget.Position=position;
                    widget.CompleteDrag();
                    if(!widget.IsEdgeHidden || !widget.Settings.EdgeHideEnabled) throw new InvalidOperationException("Drag release did not hide: "+id);
                    widget.Reveal();
                    if(widget.Position!=position) throw new InvalidOperationException("Drag reveal position changed: "+id);
                }
                widget.SetEdgeHide(false); widget.Position=new PixelPoint(x,y); widget.CompleteDrag();
                results[id+"DragEdgesAndInterior"] = !widget.IsEdgeHidden && !widget.Settings.EdgeHideEnabled;
                await widget.SaveAsync();
            }
            await CheckDragEdges("codexball");
            foreach (var entry in entries.Where(e => e.Directory is not null))
            {
                if (!widget.SelectAppearance(entry.Id)) throw new InvalidOperationException(widget.LastError);
                await Task.Delay(300);
                var player = widget.Player!;
                results[entry.Id + "Frames"] = player.RenderedFrames;
                if (player.RenderedFrames < 2) throw new InvalidOperationException("No animated frames: " + entry.Id);
                using (var image = new RenderTargetBitmap(new PixelSize((int)widget.Width * 2, (int)widget.Height * 2), new Vector(192, 192)))
                { image.Render((Visual)widget.Content!); image.Save(Path.Combine(output, entry.Id + ".png"), new PngBitmapEncoderOptions()); }
                var position = widget.Position; widget.SetEdgeHide(true); var count = player.RenderedFrames;
                await Task.Delay(120); results[entry.Id + "Paused"] = player.RenderedFrames == count;
                widget.SetEdgeHide(false); results[entry.Id + "Restored"] = widget.Position == position;
                await Task.Delay(120); results[entry.Id + "Resumed"] = player.RenderedFrames > count;
                await CheckDragEdges(entry.Id);
                // Companions stay visible after the external Codex process closes.
                vm.ApplySnapshot(CodexUsageSnapshot.Unavailable(AccountState.Unknown, "Codex is not running"), false);
                results[entry.Id + "StaysVisible"] = widget.IsVisible;
                widget.SelectAppearance(SkinCatalog.BallId);
                bool disposed = false; try { player.RenderFrame(.016f); } catch (ObjectDisposedException) { disposed = true; }
                results[entry.Id + "Released"] = disposed;
            }
            results["ringHiddenWithoutCodex"] = !widget.IsVisible;
            vm.ApplySnapshot(CodexUsageSnapshot.Unavailable(AccountState.Error, "Unavailable"), true);
            results["ringRestoredWithCodex"] = widget.IsVisible;
            results["unknownQuota"] = vm.CenterText == "--";
            widget.SelectAppearance(SkinCatalog.BallId);
            foreach(var expected in entries.Skip(1).Concat(entries.Take(1)))
            {
                var previous = widget.Player;
                widget.CycleAppearance(); await Task.Delay(120);
                if(widget.SelectedSkinId != expected.Id) throw new InvalidOperationException("Appearance cycle failed: "+widget.LastError);
                if(widget.Player is { } current && current.RenderedFrames < 2) throw new InvalidOperationException("No frames after direct character switch");
                if(previous is not null)
                {
                    bool disposed=false; try { previous.RenderFrame(.016f); } catch(ObjectDisposedException) { disposed=true; }
                    if(!disposed) throw new InvalidOperationException("Previous player was retained after direct switch");
                }
            }
            results["directCharacterCycleAndWrap"] = widget.SelectedSkinId == SkinCatalog.BallId;
            widget.SetLanguage("en-US"); results["english"] = AppLocalization.Text("Refresh") == "Refresh";
            widget.SetLanguage("zh-CN"); results["chinese"] = AppLocalization.Text("Refresh") == "刷新";
            widget.SetTopmost(false); await widget.SaveAsync(); var saved = await store.LoadAsync();
            results["singleRingAfterSelection"] = !widget.Settings.ShowResetRing;
            results["persisted"] = saved.Language == "zh-CN" && !saved.AlwaysOnTop && saved.SkinId == SkinCatalog.BallId;
            results["result"] = results.Values.OfType<bool>().All(v => v) ? "passed" : "failed";
            if (results["result"] is not "passed") ExitCode = 1;
        }
        catch (Exception e) { ExitCode = 1; results["result"] = "failed"; results["error"] = e.ToString(); }
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
