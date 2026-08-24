using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace Otium.App.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(DoubleCollection), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(WpfBrush), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public DoubleCollection? Values { get => (DoubleCollection?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public WpfBrush? LineBrush { get => (WpfBrush?)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Values is not { Count: > 1 } values || LineBrush is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double maximum = Math.Max(1, values.Max());
        double step = ActualWidth / (values.Count - 1);
        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            for (int index = 0; index < values.Count; index++)
            {
                double x = index * step;
                double y = ActualHeight - ((values[index] / maximum) * Math.Max(1, ActualHeight - 2)) - 1;
                WpfPoint point = new(x, y);
                if (index == 0) context.BeginFigure(point, false, false);
                else context.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(null, new WpfPen(LineBrush, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        }, geometry);
    }
}
