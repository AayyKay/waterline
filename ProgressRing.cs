using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;

namespace Waterline;

public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double StrokeThickness { get; set; } = 8;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size = Math.Min(ActualWidth, ActualHeight);
        var center = new System.Windows.Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(0, size / 2 - StrokeThickness);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(42, 116, 142, 192)), StrokeThickness), center, radius, radius);

        var percent = Math.Clamp(Value, 0, 100);
        if (percent <= 0) return;
        var startAngle = -90d;
        var endAngle = startAngle + Math.Min(359.99, 360d * percent / 100d);
        var start = PointAt(center, radius, startAngle);
        var end = PointAt(center, radius, endAngle);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new System.Windows.Size(radius, radius), 0, percent > 50, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        var pen = new Pen(new LinearGradientBrush(Color.FromRgb(95, 140, 255), Color.FromRgb(102, 226, 255), 0), StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, geometry);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(114, 223, 255)), new Pen(Brushes.White, 1), end, StrokeThickness * .55, StrokeThickness * .55);
    }

    private static System.Windows.Point PointAt(System.Windows.Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180d;
        return new System.Windows.Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
