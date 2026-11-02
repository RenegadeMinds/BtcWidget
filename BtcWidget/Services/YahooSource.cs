using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>
/// Yahoo Finance chart endpoint (unofficial, no key). Covers stocks on world exchanges (SHOP.TO, 005930.KS),
/// ETFs, indices (^GSPC), FX (EURUSD=X), futures (GC=F) and crypto pairs (BTC-CAD). The quote endpoint
/// needs a crumb now, so everything comes from the chart call: last price from meta, previous close and
/// history from the daily bars. Also supplies the FX rates used to show any line in another currency.
/// </summary>
public sealed class YahooSource(HttpClient http) : IPriceSource, IInstrumentSearch
{
    private static readonly HashSet<InstrumentKind> _kinds =
        [InstrumentKind.Stock, InstrumentKind.Etf, InstrumentKind.Index, InstrumentKind.Fx, InstrumentKind.Commodity, InstrumentKind.Crypto];

    public string Id => "yahoo";
    private static ProviderConfig Cfg => Providers.Get("yahoo");
    public string DisplayName => Cfg.Name;
    public string IconUri => Cfg.IconUri;
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    /// <summary>Yahoo's symbol for the line: crypto pairs are BTC-CAD, everything else is used as typed.</summary>
    private static string YSymbol(LineConfig line) =>
        line.Kind == InstrumentKind.Crypto ? $"{line.Symbol.ToUpperInvariant()}-{line.EffectiveQuote}" : line.Symbol;

    public Uri PageUrl(LineConfig line) => Cfg.PageUrl(line, ("symbol", YSymbol(line)));

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var (meta, bars) = await ChartAsync(YSymbol(line), line.HistoryKey, "1mo", ct, line);

        var price = meta.TryGetProperty("regularMarketPrice", out var rp) && rp.ValueKind == JsonValueKind.Number
            ? rp.GetDecimal()
            : bars.Count > 0 ? bars[^1].Price : throw new InvalidOperationException("Yahoo: no price");

        var currency = Json.Str(meta, "currency") ?? "USD";
        var marketTime = meta.TryGetProperty("regularMarketTime", out var mt) && mt.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(mt.GetInt64()) : DateTimeOffset.UtcNow;

        // Previous close: the bar before the current session. If the last bar belongs to the current
        // session (started within 24h of the last trade) skip it.
        decimal? prevClose = null;
        if (bars.Count >= 2)
            prevClose = marketTime - bars[^1].Timestamp < TimeSpan.FromHours(24) ? bars[^2].Price : bars[^1].Price;
        else if (meta.TryGetProperty("chartPreviousClose", out var cpc) && cpc.ValueKind == JsonValueKind.Number)
            prevClose = cpc.GetDecimal();

        double? change = prevClose is > 0 ? (double)((price - prevClose.Value) / prevClose.Value * 100m) : null;
        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow, currency);
    }

    public async Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct)
    {
        var (_, bars) = await ChartAsync(YSymbol(line), line.HistoryKey, "3mo", ct, line);
        return bars;
    }

    // ---- FX, used by PriceService to show any line in another currency ----

    /// <summary>Spot rate from one currency to another (USD→CAD = 1.37). Tries the inverse pair when needed.</summary>
    public async Task<decimal> FxRateAsync(string from, string to, CancellationToken ct)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return 1m;
        try
        {
            var (meta, bars) = await ChartAsync($"{from}{to}=X", "", "5d", ct);
            return RateFrom(meta, bars);
        }
        catch (InvalidOperationException)
        {
            var (meta, bars) = await ChartAsync($"{to}{from}=X", "", "5d", ct);
            var inverse = RateFrom(meta, bars);
            return inverse > 0 ? 1m / inverse : throw new InvalidOperationException($"Yahoo: no {from}/{to} rate");
        }

        static decimal RateFrom(JsonElement meta, List<PricePoint> bars) =>
            meta.TryGetProperty("regularMarketPrice", out var rp) && rp.ValueKind == JsonValueKind.Number ? rp.GetDecimal()
            : bars.Count > 0 ? bars[^1].Price : throw new InvalidOperationException("Yahoo: no FX price");
    }

    /// <summary>Daily rates for the last three months, oldest first.</summary>
    public async Task<IReadOnlyList<PricePoint>> FxHistoryAsync(string from, string to, CancellationToken ct)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return [];
        try
        {
            var (_, bars) = await ChartAsync($"{from}{to}=X", $"{from}{to}", "3mo", ct);
            return bars;
        }
        catch (InvalidOperationException)
        {
            var (_, bars) = await ChartAsync($"{to}{from}=X", $"{from}{to}", "3mo", ct);
            return bars.Where(b => b.Price > 0).Select(b => b with { Price = 1m / b.Price }).ToList();
        }
    }

    private async Task<(JsonElement meta, List<PricePoint> bars)> ChartAsync(string symbol, string key, string range,
        CancellationToken ct, LineConfig? line = null)
    {
        using var resp = await http.GetAsync(
            Cfg.ApiUrl("chart", ("symbol", symbol), ("range", range)), ct);
        JsonDocument doc;
        try { doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
        catch (JsonException) { throw new InvalidOperationException($"Yahoo: HTTP {(int)resp.StatusCode}"); }
        using var _ = doc;

        if (!doc.RootElement.TryGetProperty("chart", out var chart))
            throw new InvalidOperationException($"Yahoo: HTTP {(int)resp.StatusCode}");
        if (chart.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            var desc = Json.Str(err, "description") ?? "error";
            if (line is { Kind: InstrumentKind.Crypto } && desc.Contains("No data found", StringComparison.OrdinalIgnoreCase))
                throw new PairNotSupportedException($"Yahoo has no {line.PairLabel} pair");
            throw new InvalidOperationException("Yahoo: " + desc);
        }

        var result = chart.GetProperty("result")[0];
        // Clone so the element outlives the disposed document.
        var meta = result.GetProperty("meta").Clone();

        var bars = new List<PricePoint>();
        if (result.TryGetProperty("timestamp", out var times) && times.ValueKind == JsonValueKind.Array)
        {
            var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");
            var n = Math.Min(times.GetArrayLength(), closes.GetArrayLength());
            for (var i = 0; i < n; i++)
            {
                var c = closes[i];
                if (c.ValueKind != JsonValueKind.Number) continue;
                bars.Add(new PricePoint(Id, key, DateTimeOffset.FromUnixTimeSeconds(times[i].GetInt64()), c.GetDecimal()));
            }
        }
        return (meta, bars);
    }

    // ---- Search ----

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("search", ("query", query)), ct);
        if (doc is null || !doc.RootElement.TryGetProperty("quotes", out var quotes)) return [];

        var list = new List<SearchResult>();
        foreach (var q in quotes.EnumerateArray())
        {
            var kind = Json.Str(q, "quoteType") switch
            {
                "EQUITY" => InstrumentKind.Stock,
                "ETF" or "MUTUALFUND" => InstrumentKind.Etf,
                "INDEX" => InstrumentKind.Index,
                "CURRENCY" => InstrumentKind.Fx,
                "FUTURE" => InstrumentKind.Commodity,
                _ => (InstrumentKind?)null // CRYPTOCURRENCY etc. — CoinGecko covers crypto
            };
            if (kind is null) continue;
            var symbol = Json.Str(q, "symbol");
            if (string.IsNullOrEmpty(symbol)) continue;
            list.Add(new SearchResult(kind.Value,
                Json.Str(q, "shortname") ?? Json.Str(q, "longname") ?? symbol,
                symbol, null, Json.Str(q, "exchDisp"), null));
        }
        return list;
    }

    public Task<IReadOnlyList<SearchResult>> PopularAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SearchResult>>(
        [
            new(InstrumentKind.Index, "S&P 500", "^GSPC", null, "SNP", null),
            new(InstrumentKind.Index, "S&P/TSX Composite", "^GSPTSE", null, "Toronto", null),
            new(InstrumentKind.Index, "Nasdaq 100", "^NDX", null, "NASDAQ", null),
            new(InstrumentKind.Index, "Dow Jones Industrial", "^DJI", null, "DJI", null),
            new(InstrumentKind.Index, "CBOE Volatility (VIX)", "^VIX", null, "CBOE", null),
            new(InstrumentKind.Stock, "Apple", "AAPL", null, "NASDAQ", null),
            new(InstrumentKind.Stock, "Microsoft", "MSFT", null, "NASDAQ", null),
            new(InstrumentKind.Stock, "NVIDIA", "NVDA", null, "NASDAQ", null),
            new(InstrumentKind.Stock, "Tesla", "TSLA", null, "NASDAQ", null),
            new(InstrumentKind.Stock, "Shopify", "SHOP.TO", null, "Toronto", null),
            new(InstrumentKind.Stock, "Royal Bank of Canada", "RY.TO", null, "Toronto", null),
            new(InstrumentKind.Etf, "SPDR S&P 500 ETF", "SPY", null, "NYSE Arca", null),
            new(InstrumentKind.Etf, "Invesco QQQ", "QQQ", null, "NASDAQ", null),
            new(InstrumentKind.Etf, "iShares S&P/TSX 60", "XIU.TO", null, "Toronto", null),
            new(InstrumentKind.Commodity, "Gold futures", "GC=F", null, "COMEX", null),
            new(InstrumentKind.Commodity, "Silver futures", "SI=F", null, "COMEX", null),
            new(InstrumentKind.Commodity, "Crude oil WTI", "CL=F", null, "NYMEX", null),
            new(InstrumentKind.Fx, "USD / CAD", "USDCAD=X", null, "CCY", null),
            new(InstrumentKind.Fx, "EUR / USD", "EURUSD=X", null, "CCY", null),
            new(InstrumentKind.Fx, "GBP / USD", "GBPUSD=X", null, "CCY", null),
            new(InstrumentKind.Fx, "USD / JPY", "USDJPY=X", null, "CCY", null),
        ]);
}
