namespace CodexBall.Core.Utils;

public static class TimeFormatter
{
    public static string FormatReset(DateTimeOffset? resetAt, DateTimeOffset? now = null)
    {
        if (resetAt is null)
        {
            return "unknown";
        }

        var current = now ?? DateTimeOffset.Now;
        var localReset = resetAt.Value.ToLocalTime();
        var remaining = localReset - current;

        if (remaining <= TimeSpan.Zero)
        {
            return "now";
        }

        if (remaining < TimeSpan.FromHours(24))
        {
            if (remaining.TotalHours >= 1)
            {
                return $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";
            }

            return $"in {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m";
        }

        return localReset.ToString("MMM d, HH:mm");
    }
}
