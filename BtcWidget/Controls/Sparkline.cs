using System.Windows;
using System.Windows.Media;

namespace BtcWidget.Controls;

/// <summary>
/// Minimal line chart: one stroke, a faint fill under it, a dot on the last point. Values are evenly spaced;
/// NaN entries are skipped so a short history draws as a stub on the right. Redraws only when
/// <see cref="Values"/> or the brushes change, so it costs nothing while idle.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(1.25, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillOpacityProperty = DependencyProperty.Register(
        nameof(FillOpacity), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(0.18, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Opacity of the area under the line, derived from <see cref="Stroke"/>. 0 disables it.</summary>
    public double FillOpacity
    {
        get => (double)GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 60 : Math.Min(60, availableSize.Width),
            double.IsInfinity(availableSize.Height) ? 16 : Math.Min(16, availableSize.Height));

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        var w = ActualWidth;
        var h = ActualHeight;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0) return;

        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        var finite = 0;
        foreach (var v in values)
        {
            if (!double.IsFinite(v)) continue;
            finite++;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        if (finite < 2) return;

        var range = max - min;
        if (range <= 0) { range = 1; min -= 0.5; }       // flat series: draw through the middle
        var pad = StrokeThickness + 1;
        var n = values.Count;
        var points = new List<Point>(finite);
        for (var i = 0; i < n; i++)
        {
            var v = values[i];
            if (!double.IsFinite(v)) continue;
            points.Add(new Point(
                0.5 + i * (w - 1) / (n - 1),
                pad + (h - 2 * pad) * (1 - (v - min) / range)));
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            for (var i = 1; i < points.Count; i++) ctx.LineTo(points[i], true, false);
        }
        line.Freeze();

        if (FillOpacity > 0 && Stroke is SolidColorBrush sc)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, h), true, true);
                foreach (var p in points) ctx.LineTo(p, false, false);
                ctx.LineTo(new Point(points[^1].X, h), false, false);
            }
            area.Freeze();
            var c = sc.Color;
            var fill = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Clamp(FillOpacity, 0, 1)), c.R, c.G, c.B));
            fill.Freeze();
            dc.DrawGeometry(fill, null, area);
        }

        var pen = new Pen(Stroke, StrokeThickness)
        {
            LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);
        dc.DrawEllipse(Stroke, null, points[^1], StrokeThickness + 0.5, StrokeThickness + 0.5);
    }
}
