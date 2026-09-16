using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CodexBall.App.Controls;

public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressRing),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(ProgressRing),
            new FrameworkPropertyMetadata(Brushes.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ResetProgressProperty =
        DependencyProperty.Register(nameof(ResetProgress), typeof(double), typeof(ProgressRing),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ResetRingBrushProperty =
        DependencyProperty.Register(nameof(ResetRingBrush), typeof(Brush), typeof(ProgressRing),
            new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowResetRingProperty =
        DependencyProperty.Register(nameof(ShowResetRing), typeof(bool), typeof(ProgressRing),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public double ResetProgress
    {
        get => (double)GetValue(ResetProgressProperty);
        set => SetValue(ResetProgressProperty, value);
    }

    public Brush ResetRingBrush
    {
        get => (Brush)GetValue(ResetRingBrushProperty);
        set => SetValue(ResetRingBrushProperty, value);
    }

    public bool ShowResetRing
    {
        get => (bool)GetValue(ShowResetRingProperty);
        set => SetValue(ShowResetRingProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 22, 26, 32)), null, center, size / 2 - 4, size / 2 - 4);
        DrawRing(drawingContext, center, size / 2 - 4, 5, Progress, RingBrush, Color.FromArgb(70, 255, 255, 255), SweepDirection.Clockwise);
        if (ShowResetRing)
        {
            DrawRing(drawingContext, center, size / 2 - 11, 3, ResetProgress, ResetRingBrush, Color.FromArgb(42, 255, 255, 255), SweepDirection.Counterclockwise);
        }
    }

    private static void DrawRing(
        DrawingContext drawingContext,
        Point center,
        double radius,
        double thickness,
        double progressValue,
        Brush valueBrush,
        Color trackColor,
        SweepDirection sweepDirection)
    {
        if (radius <= 0)
        {
            return;
        }

        var trackPen = CreatePen(new SolidColorBrush(trackColor), thickness);
        var valuePen = CreatePen(valueBrush, thickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var progress = Math.Clamp(progressValue, 0, 100);
        if (progress <= 0)
        {
            return;
        }

        if (progress >= 99.9)
        {
            drawingContext.DrawEllipse(null, valuePen, center, radius, radius);
            return;
        }

        var startAngle = -90d;
        var sweep = 360d * progress / 100d;
        var endAngle = sweepDirection == SweepDirection.Clockwise
            ? startAngle + sweep
            : startAngle - sweep;
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var isLargeArc = progress > 50;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(end, new Size(radius, radius), 0, isLargeArc, sweepDirection, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, valuePen, geometry);
    }

    private static Pen CreatePen(Brush brush, double thickness)
        => new(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
