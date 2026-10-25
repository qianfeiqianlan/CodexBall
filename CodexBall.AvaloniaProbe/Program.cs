using CodexBall.Rendering;
using Avalonia;

namespace CodexBall.AvaloniaProbe;

internal static class Program
{
    internal static string[] Arguments = [];
    internal static bool UseCoreProfile => OperatingSystem.IsMacOS() || Arguments.Contains("--gl-core");
    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        NativeRuntime.Register();
        AppBuilder.Configure<ProbeApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}
