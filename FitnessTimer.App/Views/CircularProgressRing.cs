using System.Windows;
using System.Windows.Media;

namespace FitnessTimer.App.Views;

public sealed class CircularProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(CircularProgressRing),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double size = Math.Min(
            double.IsInfinity(availableSize.Width) ? 390 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 390 : availableSize.Height);
        return new Size(size, size);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        Point center = new(ActualWidth / 2, ActualHeight / 2);
        double radius = Math.Max(0, size / 2 - 4);
        Pen trackPen = new(FindBrush("BorderBrushSoft"), 1.2);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        double progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
        {
            return;
        }

        Pen progressPen = new(FindBrush("AccentBrush"), 7)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        drawingContext.DrawGeometry(null, progressPen, CreateArcGeometry(center, radius, progress));
    }

    private static Geometry CreateArcGeometry(Point center, double radius, double progress)
    {
        if (progress >= 0.999)
        {
            GeometryGroup fullCircle = new();
            fullCircle.Children.Add(CreateArcSegment(center, radius, 0, 180));
            fullCircle.Children.Add(CreateArcSegment(center, radius, 180, 359.99));
            return fullCircle;
        }

        double endAngle = -360 * progress;
        return CreateArcSegment(center, radius, 0, endAngle);
    }

    private static StreamGeometry CreateArcSegment(Point center, double radius, double startAngle, double endAngle)
    {
        Point start = PointOnCircle(center, radius, startAngle);
        Point end = PointOnCircle(center, radius, endAngle);
        bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

        StreamGeometry geometry = new();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(start, false, false);
        context.ArcTo(end, new Size(radius, radius), 0, isLargeArc, SweepDirection.Counterclockwise, true, false);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
    }

    private Brush FindBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as Brush ??
            Application.Current.TryFindResource(resourceKey) as Brush ??
            Brushes.Gray;
    }
}
