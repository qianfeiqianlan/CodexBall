namespace CodexBall.Core.Utils;

public enum HideEdge { Left, Right, Top, Bottom }

public sealed record EdgeHidePlacement(HideEdge Edge, double VisibleLeft, double VisibleTop, double HiddenLeft, double HiddenTop);

public static class EdgeHideGeometry
{
    // Use the drag's unclamped position. All arguments share the caller's coordinate units.
    public static bool IsAtWorkAreaEdge(double left, double top, double width, double height,
        double workLeft, double workTop, double workWidth, double workHeight, double snapDistance = 12)
    {
        if (new[] { left, top, width, height, workLeft, workTop, workWidth, workHeight, snapDistance }.Any(v => !double.IsFinite(v))
            || width <= 0 || height <= 0 || workWidth <= 0 || workHeight <= 0 || snapDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(snapDistance));
        return left - workLeft <= snapDistance || top - workTop <= snapDistance
            || workLeft + workWidth - left - width <= snapDistance
            || workTop + workHeight - top - height <= snapDistance;
    }

    public static bool IsBeyondWorkArea(double left, double top, double width, double height,
        double workLeft, double workTop, double workWidth, double workHeight)
        => left < workLeft || top < workTop
            || left + width > workLeft + workWidth || top + height > workTop + workHeight;

    public static EdgeHidePlacement Calculate(double left, double top, double width, double height,
        double workLeft, double workTop, double workWidth, double workHeight, double visibleStrip = 6)
    {
        if (new[] { left, top, width, height, workLeft, workTop, workWidth, workHeight, visibleStrip }.Any(v => !double.IsFinite(v))
            || width <= 0 || height <= 0 || workWidth <= 0 || workHeight <= 0 || visibleStrip <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Window and work area geometry must be finite with positive sizes.");
        var x = Math.Clamp(left, workLeft, workLeft + Math.Max(0, workWidth - width));
        var y = Math.Clamp(top, workTop, workTop + Math.Max(0, workHeight - height));
        var candidates = new[]
        {
            (HideEdge.Left, (x - workLeft) / workWidth),
            (HideEdge.Right, Math.Max(0, workLeft + workWidth - x - width) / workWidth),
            (HideEdge.Top, (y - workTop) / workHeight),
            (HideEdge.Bottom, Math.Max(0, workTop + workHeight - y - height) / workHeight)
        };
        // Fixed order breaks equal-distance ties deterministically.
        var edge = candidates.MinBy(c => c.Item2).Item1;
        var hiddenX = edge switch
        {
            HideEdge.Left => workLeft - width + Math.Min(visibleStrip, width),
            HideEdge.Right => workLeft + workWidth - Math.Min(visibleStrip, width),
            _ => x
        };
        var hiddenY = edge switch
        {
            HideEdge.Top => workTop - height + Math.Min(visibleStrip, height),
            HideEdge.Bottom => workTop + workHeight - Math.Min(visibleStrip, height),
            _ => y
        };
        return new(edge, x, y, hiddenX, hiddenY);
    }
}
