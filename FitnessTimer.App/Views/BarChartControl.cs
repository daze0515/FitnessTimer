using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FitnessTimer.App.Services;

namespace FitnessTimer.App.Views;

public sealed class BarChartControl : FrameworkElement
{
    private readonly List<BarHitArea> hitAreas = [];

    public static readonly DependencyProperty BarsProperty =
        DependencyProperty.Register(
            nameof(Bars),
            typeof(IEnumerable<StatisticsBar>),
            typeof(BarChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<StatisticsBar>? Bars
    {
        get => (IEnumerable<StatisticsBar>?)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 360 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? 220 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        Rect bounds = new(0, 0, ActualWidth, ActualHeight);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Brush panelBrush = FindBrush("TimerCircleBrush");
        Brush borderBrush = FindBrush("BorderBrushSoft");
        Brush labelBrush = FindBrush("MutedTextBrush");
        Brush barBrush = FindBrush("AccentBrush");
        Brush barSoftBrush = FindBrush("ChartSoftAccentBrush");

        drawingContext.DrawRoundedRectangle(
            panelBrush,
            new Pen(borderBrush, 1),
            bounds,
            8,
            8);

        hitAreas.Clear();

        List<StatisticsBar> bars = Bars?.Where(bar => bar is not null).ToList() ?? [];
        if (bars.Count == 0)
        {
            DrawCenteredText(drawingContext, "暂无统计数据", 14, labelBrush, bounds, pixelsPerDip);
            return;
        }

        double left = 34;
        double right = 18;
        double top = 20;
        double bottom = 34;
        double chartWidth = Math.Max(1, ActualWidth - left - right);
        double chartHeight = Math.Max(1, ActualHeight - top - bottom);
        int maxMinutes = Math.Max(1, bars.Max(bar => bar.TrainingMinutes));

        Pen gridPen = new(borderBrush, 1);

        for (int line = 0; line <= 2; line++)
        {
            double y = top + chartHeight * line / 2;
            drawingContext.DrawLine(gridPen, new Point(left, y), new Point(left + chartWidth, y));
        }

        double slot = chartWidth / bars.Count;
        double barWidth = Math.Max(8, Math.Min(28, slot * 0.55));

        for (int index = 0; index < bars.Count; index++)
        {
            StatisticsBar bar = bars[index];
            double x = left + slot * index + (slot - barWidth) / 2;
            double ratio = Math.Clamp(bar.TrainingMinutes / (double)maxMinutes, 0, 1);
            double height = Math.Max(bar.TrainingMinutes > 0 ? 3 : 0, chartHeight * ratio);
            double y = top + chartHeight - height;
            Rect barRect = new(x, y, barWidth, height);
            Rect hitRect = new(x, top, barWidth, chartHeight);
            hitAreas.Add(new BarHitArea(hitRect, bar));

            drawingContext.DrawRoundedRectangle(barSoftBrush, null, hitRect, 4, 4);
            drawingContext.DrawRoundedRectangle(barBrush, null, barRect, 4, 4);

            DrawText(
                drawingContext,
                bar.Date.ToString("MM/dd", CultureInfo.CurrentCulture),
                11,
                labelBrush,
                new Point(x + barWidth / 2, top + chartHeight + 8),
                TextAlignment.Center,
                pixelsPerDip);

            if (bar.TrainingMinutes > 0)
            {
                DrawText(
                    drawingContext,
                    $"{bar.TrainingMinutes}m",
                    11,
                    labelBrush,
                    new Point(x + barWidth / 2, y - 16),
                    TextAlignment.Center,
                    pixelsPerDip);
            }
        }

        DrawText(drawingContext, $"{maxMinutes}m", 11, labelBrush, new Point(8, top - 2), TextAlignment.Left, pixelsPerDip);
        DrawText(drawingContext, "0", 11, labelBrush, new Point(18, top + chartHeight - 12), TextAlignment.Left, pixelsPerDip);
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Point position = e.GetPosition(this);
        BarHitArea? hitArea = hitAreas.FirstOrDefault(area => area.Bounds.Contains(position));
        ToolTip = hitArea is null
            ? null
            : $"{hitArea.Bar.Date:yyyy-MM-dd}  训练 {hitArea.Bar.TrainingMinutes} 分钟";
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ToolTip = null;
    }

    private static void DrawCenteredText(DrawingContext drawingContext, string text, double fontSize, Brush brush, Rect bounds, double pixelsPerDip)
    {
        FormattedText formatted = CreateText(text, fontSize, brush, TextAlignment.Center, pixelsPerDip);
        drawingContext.DrawText(formatted, new Point(bounds.Left + (bounds.Width - formatted.Width) / 2, bounds.Top + (bounds.Height - formatted.Height) / 2));
    }

    private static void DrawText(DrawingContext drawingContext, string text, double fontSize, Brush brush, Point point, TextAlignment alignment, double pixelsPerDip)
    {
        FormattedText formatted = CreateText(text, fontSize, brush, alignment, pixelsPerDip);
        double x = alignment == TextAlignment.Center ? point.X - formatted.Width / 2 : point.X;
        drawingContext.DrawText(formatted, new Point(x, point.Y));
    }

    private static FormattedText CreateText(string text, double fontSize, Brush brush, TextAlignment alignment, double pixelsPerDip)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"),
            fontSize,
            brush,
            pixelsPerDip)
        {
            TextAlignment = alignment
        };
    }

    private Brush FindBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as Brush ??
            Application.Current.TryFindResource(resourceKey) as Brush ??
            Brushes.Gray;
    }

    private sealed record BarHitArea(Rect Bounds, StatisticsBar Bar);
}
