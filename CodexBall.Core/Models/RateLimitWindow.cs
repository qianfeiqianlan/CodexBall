namespace CodexBall.Core.Models;

public sealed record RateLimitWindow(
    double UsedPercent,
    int WindowDurationMins,
    DateTimeOffset? ResetsAt)
{
    public int RemainingPercent => (int)Math.Round(Math.Clamp(100 - UsedPercent, 0, 100));
}
