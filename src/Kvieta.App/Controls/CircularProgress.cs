using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace Kvieta.App.Controls;

public sealed class CircularProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CircularProgress),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(CircularProgress),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(WpfBrush), typeof(CircularProgress),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IndicatorBrushProperty = DependencyProperty.Register(
        nameof(IndicatorBrush), typeof(WpfBrush), typeof(CircularProgress),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(CircularProgress),
        new FrameworkPropertyMetadata(5d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public WpfBrush? TrackBrush { get => (WpfBrush?)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public WpfBrush? IndicatorBrush { get => (WpfBrush?)GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double thickness = Math.Max(1, StrokeThickness);
        double radius = Math.Max(0, (Math.Min(ActualWidth, ActualHeight) - thickness) / 2);
        WpfPoint center = new(ActualWidth / 2, ActualHeight / 2);
        WpfPen trackPen = CreatePen(TrackBrush, thickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        double progress = Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1);
        if (progress <= 0 || IndicatorBrush is null)
        {
            return;
        }

        WpfPen indicatorPen = CreatePen(IndicatorBrush, thickness);
        if (progress >= 0.999)
        {
            drawingContext.DrawEllipse(null, indicatorPen, center, radius, radius);
            return;
        }

        double startAngle = -90;
        double endAngle = startAngle + (progress * 360);
        WpfPoint start = PointOnCircle(center, radius, startAngle);
        WpfPoint end = PointOnCircle(center, radius, endAngle);
        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new WpfSize(radius, radius), 0, progress > 0.5,
                SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(null, indicatorPen, geometry);
    }

    private static WpfPen CreatePen(WpfBrush? brush, double thickness) => new(brush, thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    };

    private static WpfPoint PointOnCircle(WpfPoint center, double radius, double angle)
    {
        double radians = angle * Math.PI / 180;
        return new WpfPoint(center.X + (Math.Cos(radians) * radius), center.Y + (Math.Sin(radians) * radius));
    }
}
