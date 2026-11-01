using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BtcWidget.Converters;

/// <summary>double? percent → "+2.14%" / "-0.80%" / "—". Set <see cref="Format"/> to "+0.0;-0.0" for one decimal.</summary>
public sealed class PercentTextConverter : IValueConverter
{
    public string Suffix { get; set; } = "%";
    public string Format { get; set; } = "+0.00;-0.00";
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is double d ? d.ToString(Format, CultureInfo.InvariantCulture) + Suffix : "—";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>double? percent → green / red / neutral brush.</summary>
public sealed class PercentBrushConverter : IValueConverter
{
    public Brush Up { get; set; } = Frozen(0x4C, 0xD9, 0x64);
    public Brush Down { get; set; } = Frozen(0xFF, 0x5A, 0x5F);
    public Brush Flat { get; set; } = Frozen(0xA0, 0xA0, 0xA0);
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        double d when d > 0.005 => Up,
        double d when d < -0.005 => Down,
        _ => Flat
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

/// <summary>decimal? price → "$109,842" (no cents above 1000, 2 decimals below).</summary>
public sealed class PriceTextConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        decimal d when d >= 1000 => d.ToString("$#,##0", CultureInfo.InvariantCulture),
        decimal d => d.ToString("$#,##0.00", CultureInfo.InvariantCulture),
        _ => "—"
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>bool → Visibility. <see cref="Invert"/> flips it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        (value is true) != Invert ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>List height setting → MaxHeight: 0 (auto) becomes unlimited.</summary>
public sealed class MaxHeightConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is double d && d > 0 ? d : double.PositiveInfinity;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>List height setting → "auto" or "320 px".</summary>
public sealed class ListHeightTextConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is double d && d > 0 ? $"{d:0} px" : "auto";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>double × Parameter (e.g. FontSize × 0.8 for secondary text).</summary>
public sealed class ScaleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var factor = p is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 1.0;
        return value is double d ? Math.Max(1.0, d * factor) : value ?? 12.0;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>null → Collapsed, anything else → Visible (invert with Parameter="invert").</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var visible = value is not null;
        if (p is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
