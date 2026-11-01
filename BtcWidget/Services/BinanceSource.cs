using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Binance public market data. "USD" lines use the USDT market. Note: binance.com geo-blocks some regions.</summary>
public sealed class BinanceSource(HttpClient http) : IPriceSource
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Crypto];

    public string Id => "binance";
    public string DisplayName => "Binance";
    public string IconUri => "pack://application:,,,/Assets/binance.png";
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    private static string QuoteAsset(LineConfig line) => line.EffectiveQuote == "USD" ? "USDT" : line.EffectiveQuote;
    private static string Pair(LineConfig line) => line.Symbol.ToUpperInvariant() + QuoteAsset(line);

    public Uri PageUrl(LineConfig line) => new($"https://www.binance.com/en/trade/{line.Symbol.ToUpperInvariant()}_{QuoteAsset(line)}");

    /// <summary>GET that reads the JSON body even on 4xx: Binance reports an unknown pair as 400 + {"code":-1121}.</summary>
    private async Task<JsonDocument> GetAsync(string url, LineConfig line, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct);
        JsonDocument doc;
        try { doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
        catch (JsonException) { throw new InvalidOperationException($"Binance: HTTP {(int)resp.StatusCode}"); }
        if (doc.RootElement.ValueKind == JsonValueKind.Object) ThrowIfError(doc, line);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"Binance: HTTP {(int)resp.StatusCode}");
        return doc;
    }

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        using var doc = await GetAsync($"https://api.binance.com/api/v3/ticker/24hr?symbol={Pair(line)}", line, ct);

        var price = Json.Dec(doc.RootElement.GetProperty("lastPrice"));
        var change = Json.Pct(doc.RootElement.GetProperty("priceChangePercent"));
        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow, line.EffectiveQuote);
    }

    public async Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct)
    {
        using var doc = await GetAsync($"https://api.binance.com/api/v3/klines?symbol={Pair(line)}&interval=1h&limit=720", line, ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

        var list = new List<PricePoint>(doc.RootElement.GetArrayLength());
        foreach (var k in doc.RootElement.EnumerateArray())
        {
            // [openTime(ms), open, high, low, close, volume, closeTime, ...]
            var ts = DateTimeOffset.FromUnixTimeMilliseconds(k[0].GetInt64());
            list.Add(new PricePoint(Id, line.HistoryKey, ts, Json.Dec(k[4])));
        }
        return list;
    }

    private static void ThrowIfError(JsonDocument doc, LineConfig line)
    {
        if (Json.Str(doc.RootElement, "msg") is not { } msg) return;
        if (msg.Contains("Invalid symbol", StringComparison.OrdinalIgnoreCase))
            throw new PairNotSupportedException($"Binance has no {line.PairLabel} market");
        throw new InvalidOperationException("Binance: " + msg);
    }
}
