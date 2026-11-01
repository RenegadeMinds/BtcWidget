using System.Globalization;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Price and percent text shared by the rows, tooltips and alert messages.</summary>
public static class PriceFormat
{
    /// <summary>Fiat currencies we recognise, with the prefix used when showing a price in them.</summary>
    private static readonly Dictionary<string, string> Fiat = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£", ["JPY"] = "¥", ["CAD"] = "C$", ["AUD"] = "A$", ["CHF"] = "CHF ",
        ["NZD"] = "NZ$", ["HKD"] = "HK$", ["SGD"] = "S$", ["CNY"] = "CN¥", ["INR"] = "₹", ["KRW"] = "₩", ["MXN"] = "MX$",
        ["BRL"] = "R$", ["SEK"] = "kr ", ["NOK"] = "kr ", ["DKK"] = "kr ", ["PLN"] = "zł ", ["ZAR"] = "R ", ["TRY"] = "₺",
        ["RUB"] = "₽", ["CZK"] = "Kč ", ["HUF"] = "Ft ", ["ILS"] = "₪", ["AED"] = "AED ", ["TWD"] = "NT$", ["THB"] = "฿",
        ["PHP"] = "₱", ["IDR"] = "Rp ", ["MYR"] = "RM ", ["VND"] = "₫", ["ARS"] = "AR$", ["CLP"] = "CL$", ["COP"] = "CO$",
    };

    public static bool IsFiat(string code) => Fiat.ContainsKey(code);

    /// <summary>"$109,842", "C$150,210", "0.03712 BTC"; indices get no currency symbol.</summary>
    public static string Price(decimal p, string currency = "USD", InstrumentKind kind = InstrumentKind.Crypto)
    {
        var digits = p >= 1000 ? "#,##0" : p >= 1 ? "#,##0.00" : p >= 0.01m ? "0.0000" : p >= 0.0001m ? "0.000000" : "0.00000000";
        var number = p.ToString(digits, CultureInfo.InvariantCulture);
        if (kind == InstrumentKind.Index) return number;
        return Fiat.TryGetValue(currency, out var prefix) ? prefix + number : $"{number} {currency}";
    }

    /// <summary>"+2.1%", "-0.8%", "—".</summary>
    public static string Percent(double? v, int decimals = 1) =>
        v is null ? "—" : v.Value.ToString(decimals == 1 ? "+0.0;-0.0" : "+0.00;-0.00", CultureInfo.InvariantCulture) + "%";
}
