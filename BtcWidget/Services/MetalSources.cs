using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>
/// Precious-metal spot prices. Which metals exist, their codes, and which providers carry them all come from
/// providers.json → metals. Neither spot source offers free history, so both backfill from the matching
/// Yahoo futures contract (gold GC=F, silver SI=F…) in USD; the pipeline converts that to the line's currency.
/// Futures sit a little above spot, so the first month of sparkline is futures-shaped, then the widget's own
/// spot readings take over.
/// </summary>
public static class Metals
{
    public static MetalConfig? Find(string symbol) =>
        Providers.Metals.FirstOrDefault(m => m.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

    public static MetalConfig Require(LineConfig line, string providerId)
    {
        var m = Find(line.Symbol) ?? throw new PairNotSupportedException($"{line.Symbol} is not a metal in providers.json");
        if (!m.Providers.Contains(providerId, StringComparer.OrdinalIgnoreCase))
            throw new PairNotSupportedException($"{providerId} does not carry {m.Name}");
        return m;
    }

    public static SearchResult ToResult(MetalConfig m) => new(InstrumentKind.Metal, m.Name, m.Symbol, m.Slug, "Spot", null);

    /// <summary>"gold", "Gold", "xau", "silv" → matching metals.</summary>
    public static IReadOnlyList<SearchResult> Search(string query)
    {
        var q = query.Trim();
        if (q.Length < 2) return [];
        return Providers.Metals
            .Where(m => m.Symbol.Equals(q, StringComparison.OrdinalIgnoreCase)
                        || m.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                        || m.Slug.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .Select(ToResult).ToList();
    }

    /// <summary>Daily closes of the metal's futures contract on Yahoo, in USD. Other currencies: the caller converts.</summary>
    public static async Task<IReadOnlyList<PricePoint>> FuturesHistoryAsync(YahooSource yahoo, LineConfig line, MetalConfig m, CancellationToken ct)
    {
        if (!line.EffectiveQuote.Equals("USD", StringComparison.OrdinalIgnoreCase))
            throw new PairNotSupportedException("metal history is in USD");
        if (string.IsNullOrWhiteSpace(m.Futures)) return [];
        var futures = new LineConfig { Kind = InstrumentKind.Commodity, Symbol = m.Futures, Name = m.Name };
        return await yahoo.GetHistoryAsync(futures, ct);
    }
}

/// <summary>
/// Kitco's price feed (the unofficial endpoint behind its charts, no key). Gold, silver, platinum, palladium in
/// USD, CAD, EUR and more. Returns bid / mid / ask, the change since the previous close, and the day's range.
/// </summary>
public sealed class KitcoSource(HttpClient http, YahooSource yahoo) : IPriceSource, IInstrumentSearch
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Metal];
    private static ProviderConfig Cfg => Providers.Get("kitco");

    public string Id => "kitco";
    public string DisplayName => Cfg.Name;
    public string IconUri => Cfg.IconUri;
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    public Uri PageUrl(LineConfig line) => Cfg.PageUrl(line, ("coinId", Metals.Find(line.Symbol)?.Slug ?? line.Symbol.ToLowerInvariant()));

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var m = Metals.Require(line, Id);
        if (string.IsNullOrWhiteSpace(m.Kitco)) throw new PairNotSupportedException($"Kitco code for {m.Name} missing in providers.json");
        var quote = line.EffectiveQuote.Length > 0 ? line.EffectiveQuote : "USD";
        using var resp = await http.GetAsync(Cfg.ApiUrl("quote", ("code", m.Kitco), ("QUOTE", quote)), ct);
        // An unknown currency comes back as a 404 page: let the pipeline fetch USD and convert instead.
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) throw new PairNotSupportedException($"Kitco has no {m.Name} in {quote}");
        resp.EnsureSuccessStatusCode();
        var text = await resp.Content.ReadAsStringAsync(ct);

        // AU,CAD,OUNCE,2026-11-20 11:26:00,bid,mid,ask,change,change%,low,high
        var f = text.Trim().Split(',');
        if (f.Length < 9 || !f[0].Equals(m.Kitco, StringComparison.OrdinalIgnoreCase) || !f[1].Equals(quote, StringComparison.OrdinalIgnoreCase))
            throw new PairNotSupportedException($"Kitco has no {m.Name} in {quote}");
        var price = decimal.Parse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture);
        double? change = double.TryParse(f[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : null;
        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow, quote.ToUpperInvariant());
    }

    public Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct) =>
        Metals.FuturesHistoryAsync(yahoo, line, Metals.Require(line, Id), ct);

    public Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct) =>
        Task.FromResult<bool?>(Metals.Find(line.Symbol)?.Providers.Contains(Id, StringComparer.OrdinalIgnoreCase) ?? false);

    // Metal search lives here (one searcher is enough; the merge de-duplicates anyway).
    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct) => Task.FromResult(Metals.Search(query));
    public Task<IReadOnlyList<SearchResult>> PopularAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SearchResult>>(Providers.Metals.Select(Metals.ToResult).ToList());
}

/// <summary>gold-api.com (no key): spot gold, silver, platinum, palladium in any major currency. Price only.</summary>
public sealed class GoldApiSource(HttpClient http, YahooSource yahoo) : IPriceSource
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Metal];
    private static ProviderConfig Cfg => Providers.Get("goldapi");

    public string Id => "goldapi";
    public string DisplayName => Cfg.Name;
    public string IconUri => Cfg.IconUri;
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    public Uri PageUrl(LineConfig line) => Cfg.PageUrl(line, ("coinId", Metals.Find(line.Symbol)?.Slug ?? line.Symbol.ToLowerInvariant()));

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var m = Metals.Require(line, Id);
        var quote = line.EffectiveQuote.Length > 0 ? line.EffectiveQuote : "USD";
        using var resp = await http.GetAsync(Cfg.ApiUrl("price", ("symbol", m.Symbol), ("QUOTE", quote)), ct);
        if ((int)resp.StatusCode is 400 or 404) throw new PairNotSupportedException($"gold-api has no {m.Name} in {quote}");
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("price", out var p) || p.ValueKind != JsonValueKind.Number
            || !string.Equals(Json.Str(root, "currency"), quote, StringComparison.OrdinalIgnoreCase))
            throw new PairNotSupportedException($"gold-api has no {m.Name} in {quote}");
        return new PriceQuote(Id, line.Symbol, p.GetDecimal(), null, DateTimeOffset.UtcNow, quote.ToUpperInvariant());
    }

    public Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct) =>
        Metals.FuturesHistoryAsync(yahoo, line, Metals.Require(line, Id), ct);

    public Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct) =>
        Task.FromResult<bool?>(Metals.Find(line.Symbol)?.Providers.Contains(Id, StringComparer.OrdinalIgnoreCase) ?? false);
}
