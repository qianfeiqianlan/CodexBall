namespace CodexBall.Core.Models;

public sealed record CodexUsageSnapshot(
    AccountState AccountState,
    RateLimitWindow? ShortWindow,
    RateLimitWindow? LongWindow,
    DateTimeOffset LastUpdatedAt,
    string? Message = null)
{
    public static CodexUsageSnapshot Unavailable(AccountState state, string message)
        => new(state, null, null, DateTimeOffset.Now, message);
}
