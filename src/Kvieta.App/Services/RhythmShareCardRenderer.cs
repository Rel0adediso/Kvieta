using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace Kvieta.App.Services;

public static class RhythmShareCardRenderer
{
    public static BitmapSource Create(
        string title,
        string streak,
        string best,
        string weeklyChange,
        string focus,
        bool english)
    {
        const int width = 1200;
        const int height = 630;
        DrawingVisual visual = new();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRoundedRectangle(
                new SolidColorBrush(MediaColor.FromRgb(26, 30, 27)),
                null,
                new Rect(0, 0, width, height),
                34,
                34);
            drawing.DrawRoundedRectangle(
                new SolidColorBrush(MediaColor.FromRgb(190, 202, 137)),
                null,
                new Rect(66, 62, 14, 506),
                7,
                7);

            DrawText(drawing, title, 66, 62, 48, FontWeights.SemiBold, MediaColor.FromRgb(240, 243, 235));
            DrawText(drawing,
                english ? "A gentler digital rhythm, kept on this device." : "Bu cihazda kalan daha sakin bir dijital ritim.",
                68, 132, 24, FontWeights.Normal, MediaColor.FromRgb(171, 180, 168));

            DrawMetric(drawing, english ? "STREAK" : "SERİ", streak, 68, 242);
            DrawMetric(drawing, english ? "BEST" : "EN İYİ", best, 344, 242);
            DrawMetric(drawing, english ? "WEEKLY CHANGE" : "HAFTALIK DEĞİŞİM", weeklyChange, 620, 242);
            DrawMetric(drawing, english ? "FOCUS" : "ODAK", focus, 896, 242);

            DrawText(drawing, "Kvieta", 68, 516, 30, FontWeights.SemiBold, MediaColor.FromRgb(190, 202, 137));
            DrawText(drawing,
                english ? "Private by design · no app names shared" : "Gizlilik tasarımın parçası · uygulama adları paylaşılmaz",
                908, 528, 17, FontWeights.Normal, MediaColor.FromRgb(171, 180, 168), TextAlignment.Right);
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawMetric(DrawingContext drawing, string label, string value, double x, double y)
    {
        DrawText(drawing, label, x, y, 17, FontWeights.SemiBold, MediaColor.FromRgb(171, 180, 168));
        DrawText(drawing, value, x, y + 42, 37, FontWeights.SemiBold, MediaColor.FromRgb(240, 243, 235));
    }

    private static void DrawText(
        DrawingContext drawing,
        string text,
        double x,
        double y,
        double size,
        FontWeight weight,
        MediaColor color,
        TextAlignment alignment = TextAlignment.Left)
    {
        FormattedText formatted = new(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            1)
        {
            TextAlignment = alignment,
            MaxTextWidth = alignment == TextAlignment.Right ? 430 : 1080
        };
        drawing.DrawText(formatted, new System.Windows.Point(x, y));
    }
}
