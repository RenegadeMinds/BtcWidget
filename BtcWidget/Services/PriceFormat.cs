using System.Globalization;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Price and percent text shared by the rows, tooltips and alert messages.</summary>
public static class PriceFormat
{
    /// <summary>Fiat currencies and their price prefixes come from currencies.json.</summary>
    public static bool IsFiat(string code) => Currencies.IsFiat(code);

    /// <summary>"$109,842", "C$150,210", "0.03712 BTC"; indices get no currency symbol.</summary>
    public static string Price(decimal p, string currency = "USD", InstrumentKind kind = InstrumentKind.Crypto)
    {
        var digits = p >= 1000 ? "#,##0" : p >= 1 ? "#,##0.00" : p >= 0.01m ? "0.0000" : p >= 0.0001m ? "0.000000" : "0.00000000";
        var number = p.ToString(digits, CultureInfo.InvariantCulture);
        if (kind == InstrumentKind.Index) return number;
        return Currencies.Prefix(currency) is { } prefix ? prefix + number : $"{number} {currency}";
    }

    /// <summary>"+2.1%", "-0.8%", "—".</summary>
    public static string Percent(double? v, int decimals = 1) =>
        v is null ? "—" : v.Value.ToString(decimals == 1 ? "+0.0;-0.0" : "+0.00;-0.00", CultureInfo.InvariantCulture) + "%";
}
