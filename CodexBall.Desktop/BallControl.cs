using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CodexBall.Desktop;

internal sealed class BallControl(StatusBallViewModel viewModel, Func<bool> dualRing) : Control
{
    public override void Render(DrawingContext context)
    {
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#DC161A20")), null, new Point(32, 32), 28, 28);
        Ring(context, 28, 5, viewModel.Progress, viewModel.RingBrush, false);
        if (dualRing()) Ring(context, 21, 3, viewModel.ResetProgress, viewModel.ResetRingBrush, true);
    }
    private static void Ring(DrawingContext c, double radius, double thickness, double progress, IBrush brush, bool reverse)
    {
        var center = new Point(32, 32);
        c.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#40FFFFFF")), thickness), center, radius, radius);
        if (progress <= 0) return;
        var pen = new Pen(brush, thickness, lineCap: PenLineCap.Round);
        if (progress >= 99.9) { c.DrawEllipse(null, pen, center, radius, radius); return; }
        var angle = (-90 + (reverse ? -1 : 1) * progress * 3.6) * Math.PI / 180;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(32, 32 - radius), false);
            path.ArcTo(new Point(32 + radius * Math.Cos(angle), 32 + radius * Math.Sin(angle)), new Size(radius, radius), 0, progress > 50, reverse ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
            path.EndFigure(false);
        }
        c.DrawGeometry(null, pen, geometry);
    }
}
