namespace CodexBall.App.Services;

public sealed class AppSettings
{
    public string Language { get; set; } = "system";

    public double? Left { get; set; }

    public double? Top { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public bool EdgeHideEnabled { get; set; }

    public bool ShowResetRing { get; set; } = true;

    public double Opacity { get; set; } = 1.0;

    public bool CompanionMode { get; set; }

    public string SkinId { get; set; } = "codexball";
}
