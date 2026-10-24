namespace CodexBall.Core.Utils;

public static class TimeFormatter
{
    public static string FormatReset(DateTimeOffset? resetAt, DateTimeOffset? now = null, bool chinese = false)
    {
        if (resetAt is null)
        {
            return chinese ? "未知" : "unknown";
        }

        var current = now ?? DateTimeOffset.Now;
        var localReset = resetAt.Value.ToLocalTime();
        var remaining = localReset - current;

        if (remaining <= TimeSpan.Zero)
        {
            return chinese ? "现在" : "now";
        }

        if (remaining < TimeSpan.FromHours(24))
        {
            if (remaining.TotalHours >= 1)
            {
                return chinese ? $"{(int)remaining.TotalHours}小时{remaining.Minutes}分钟后" : $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";
            }

            return chinese ? $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}分钟后" : $"in {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m";
        }

        return localReset.ToString(chinese ? "M月d日 HH:mm" : "MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }
}
