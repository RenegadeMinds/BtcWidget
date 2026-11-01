using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Nasdaq.com quote API (unofficial, no key). US stocks and ETFs. No history.</summary>
public sealed class NasdaqSource(HttpClient http) : IPriceSource
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Stock, InstrumentKind.Etf];

    public string Id => "nasdaq";
    public string DisplayName => "Nasdaq";
    public string IconUri => "pack://application:,,,/Assets/nasdaq.png";
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    public Uri PageUrl(LineConfig line) =>
        new($"https://www.nasdaq.com/market-activity/{(line.Kind == InstrumentKind.Etf ? "etf" : "stocks")}/{line.Symbol.ToLowerInvariant()}");

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var assetClass = line.Kind == InstrumentKind.Etf ? "etf" : "stocks";
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            $"https://api.nasdaq.com/api/quote/{Uri.EscapeDataString(line.Symbol)}/info?assetclass={assetClass}", ct)
            ?? throw new InvalidOperationException("Nasdaq returned no body");

        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            var msg = root.TryGetProperty("status", out var st) && st.TryGetProperty("bCodeMessage", out var bm) &&
                      bm.ValueKind == JsonValueKind.Array && bm.GetArrayLength() > 0
                ? Json.Str(bm[0], "errorMessage") : null;
            throw new InvalidOperationException("Nasdaq: " + (msg ?? "no data"));
        }

        var primary = data.GetProperty("primaryData");
        var price = Json.Dec(primary.GetProperty("lastSalePrice"));
        var change = primary.TryGetProperty("percentageChange", out var pc) ? Json.Pct(pc) : null;
        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow);
    }
}
