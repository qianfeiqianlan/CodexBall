using System.Globalization;

namespace CodexBall.AvaloniaProbe;

internal static class AppLocalization
{
    private static readonly bool Chinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
    public static string Text(string zh, string en) => Chinese ? zh : en;
    public static string Title => Text("CodexBall · Avalonia 验证", "CodexBall · Avalonia probe");
}
