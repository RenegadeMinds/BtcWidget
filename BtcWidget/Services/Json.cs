using System.Globalization;
using System.Text.Json;

namespace BtcWidget.Services;

/// <summary>Small helpers for the loosely typed JSON most market APIs return.</summary>
internal static class Json
{
    /// <summary>Number or numeric string → decimal.</summary>
    public static decimal Dec(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDecimal(),
        JsonValueKind.String => decimal.Parse(Clean(e.GetString()!), NumberStyles.Float, CultureInfo.InvariantCulture),
        _ => throw new FormatException($"Expected a number, got {e.ValueKind}")
    };

    /// <summary>Like <see cref="Dec"/> but tolerant: null for anything unparsable ("N/A", "UNCH", missing).</summary>
    public static double? Pct(JsonElement e)
    {
        try
        {
            if (e.ValueKind == JsonValueKind.Number) return e.GetDouble();
            if (e.ValueKind == JsonValueKind.String &&
                double.TryParse(Clean(e.GetString()!), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
        }
        catch { /* fall through */ }
        return null;
    }

    public static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Clean(string s) => s.Replace("$", "").Replace(",", "").Replace("%", "").Replace("+", "").Trim();
}
