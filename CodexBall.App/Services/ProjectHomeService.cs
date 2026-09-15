using System.Diagnostics;

namespace CodexBall.App.Services;

public static class ProjectHomeService
{
    private const string HomeUrl = "https://github.com/qianfeiqianlan/CodexBall";

    public static void Open()
    {
        Process.Start(new ProcessStartInfo(HomeUrl)
        {
            UseShellExecute = true
        });
    }
}
