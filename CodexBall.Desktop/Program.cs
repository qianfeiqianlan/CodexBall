using Avalonia;
using CodexBall.Rendering;

namespace CodexBall.Desktop;

internal static class Program
{
    public static bool RestoreSmoke { get; private set; }
    public static bool Smoke { get; private set; }
    public static bool ForceCore { get; private set; }
    private static async Task CheckCodexAsync()
    {
        var output = Path.GetFullPath("artifacts/desktop-smoke"); Directory.CreateDirectory(output);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var executable = await CodexBall.Core.Codex.CodexLocator.LocateAsync(timeout.Token);
        await using var service = new CodexBall.Core.Codex.CodexUsageService();
        var snapshot = await service.RefreshAsync(timeout.Token);
        File.WriteAllText(Path.Combine(output, "codex.json"), System.Text.Json.JsonSerializer.Serialize(new { executable = executable?.Path, state = snapshot.AccountState.ToString(), snapshot.Message, hasShortWindow = snapshot.ShortWindow is not null, hasLongWindow = snapshot.LongWindow is not null }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = snapshot.AccountState == CodexBall.Core.Models.AccountState.Available ? 0 : 1;
    }
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--check-codex")) { CheckCodexAsync().GetAwaiter().GetResult(); return; }
        RestoreSmoke = args.Contains("--smoke-restore");
        Smoke = args.Contains("--smoke") || RestoreSmoke; ForceCore = args.Contains("--gl-core");
        NativeRuntime.Register();
        AppBuilder.Configure<DesktopApp>().UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = false, DisableDefaultApplicationMenuItems = true })
            .LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}
