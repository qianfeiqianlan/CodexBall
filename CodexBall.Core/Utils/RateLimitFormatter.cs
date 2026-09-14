using CodexBall.Core.Models;

namespace CodexBall.Core.Utils;

public static class RateLimitFormatter
{
    public static string FormatWindowLabel(int windowDurationMins)
    {
        if (windowDurationMins <= 0)
        {
            return "Unknown";
        }

        if (windowDurationMins % 10080 == 0)
        {
            var weeks = windowDurationMins / 10080;
            return weeks == 1 ? "7d" : $"{weeks * 7}d";
        }

        if (windowDurationMins % 1440 == 0)
        {
            return $"{windowDurationMins / 1440}d";
        }

        if (windowDurationMins % 60 == 0)
        {
            return $"{windowDurationMins / 60}h";
        }

        return $"{windowDurationMins}m";
    }

    public static string FormatTooltip(CodexUsageSnapshot snapshot)
    {
        if (snapshot.ShortWindow is null)
        {
            return $"Codex{Environment.NewLine}{snapshot.Message ?? "Unavailable"}";
        }

        var lines = new List<string>
        {
            "Codex",
            string.Empty,
            FormatWindowLine(snapshot.ShortWindow)
        };

        if (snapshot.LongWindow is not null && snapshot.LongWindow != snapshot.ShortWindow)
        {
            lines.Add(FormatWindowLine(snapshot.LongWindow));
        }

        lines.Add(string.Empty);
        lines.Add($"{FormatWindowLabel(snapshot.ShortWindow.WindowDurationMins)} resets {TimeFormatter.FormatReset(snapshot.ShortWindow.ResetsAt)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatWindowLine(RateLimitWindow window)
        => $"{FormatWindowLabel(window.WindowDurationMins),-8} {window.RemainingPercent}% remaining";
}
