using CodexBall.Core.Models;

namespace CodexBall.Core.Companion;

public sealed class QuotaReactionTracker
{
    private RateLimitWindow? _previous;
    private bool _low;

    public CompanionAction? Observe(RateLimitWindow? window)
    {
        if (window is null) { _previous = null; return null; }
        var previous = _previous;
        _previous = window;
        if (previous is null) { _low = window.RemainingPercent < 20; return null; }
        if (window.RemainingPercent >= 25) _low = false;
        if (!_low && window.RemainingPercent < 20)
        {
            _low = true;
            return CompanionAction.QuotaLow;
        }
        if (previous.WindowDurationMins == window.WindowDurationMins
            && previous.ResetsAt is { } oldReset && window.ResetsAt is { } newReset
            && oldReset <= DateTimeOffset.UtcNow && newReset > oldReset
            && window.RemainingPercent > previous.RemainingPercent)
            return CompanionAction.QuotaRecovered;
        return null;
    }
}
