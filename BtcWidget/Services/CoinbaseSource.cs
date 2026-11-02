using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Coinbase Exchange public market data (no key).</summary>
public sealed class CoinbaseSource(HttpClient http) : IPriceSource
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Crypto];
    private HashSet<string>? _products;

    public string Id => "coinbase";
    private static ProviderConfig Cfg => Providers.Get("coinbase");
    public string DisplayName => Cfg.Name;
    public string IconUri => Cfg.IconUri;
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    /// <summary>Coinbase product id: BTC-USD, ETH-BTC, BTC-EUR.</summary>
    private static string Product(LineConfig line) => $"{line.Symbol.ToUpperInvariant()}-{line.EffectiveQuote}";

    public Uri PageUrl(LineConfig line) => Cfg.PageUrl(line, ("product", Product(line)));

    private async Task<string> ProductAsync(LineConfig line, CancellationToken ct)
    {
        var product = Product(line);
        var products = await GetProductsAsync(ct);
        if (products is not null && !products.Contains(product))
            throw new PairNotSupportedException($"Coinbase has no {line.PairLabel} market");
        return product;
    }

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var product = await ProductAsync(line, ct);
        using var ticker = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("ticker", ("product", product)), ct)
            ?? throw new InvalidOperationException("Coinbase returned no body");
        if (Json.Str(ticker.RootElement, "message") is { } msg)
        {
            if (msg.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                throw new PairNotSupportedException($"Coinbase has no {line.PairLabel} market");
            throw new InvalidOperationException("Coinbase: " + msg);
        }
        var price = Json.Dec(ticker.RootElement.GetProperty("price"));

        double? change = null;
        try
        {
            using var stats = await http.GetFromJsonAsync<JsonDocument>(
                Cfg.ApiUrl("stats", ("product", product)), ct);
            if (stats is not null && stats.RootElement.TryGetProperty("open", out var o))
            {
                var open = Json.Dec(o); // stats "open" is the price 24h ago
                if (open > 0) change = (double)((price - open) / open * 100m);
            }
        }
        catch { /* 24h change is optional */ }

        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow, line.EffectiveQuote);
    }

    public async Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct)
    {
        var product = await ProductAsync(line, ct);
        // Max 300 candles per call; 6h granularity covers ~75 days, enough for the 30d figure.
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("candles", ("product", product)), ct)
            ?? throw new InvalidOperationException("Coinbase returned no body");
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

        var list = new List<PricePoint>(doc.RootElement.GetArrayLength());
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            // [time, low, high, open, close, volume]
            var ts = DateTimeOffset.FromUnixTimeSeconds(c[0].GetInt64());
            list.Add(new PricePoint(Id, line.HistoryKey, ts, Json.Dec(c[4])));
        }
        return list;
    }

    public async Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct)
    {
        if (line.Kind != InstrumentKind.Crypto) return false;
        var products = await GetProductsAsync(ct);
        return products?.Contains(Product(line));
    }

    private async Task<HashSet<string>?> GetProductsAsync(CancellationToken ct)
    {
        if (_products is not null) return _products;
        try
        {
            using var doc = await http.GetFromJsonAsync<JsonDocument>(Cfg.ApiUrl("products"), ct);
            if (doc is null) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateArray())
                if (Json.Str(p, "id") is { } id && Json.Str(p, "status") == "online") set.Add(id);
            return _products = set;
        }
        catch
        {
            return null;
        }
    }
}
