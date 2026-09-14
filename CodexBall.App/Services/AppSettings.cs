namespace CodexBall.App.Services;

public sealed class AppSettings
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public bool EdgeHideEnabled { get; set; }

    public double Opacity { get; set; } = 1.0;
}
