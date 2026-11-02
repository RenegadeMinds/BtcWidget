using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

public sealed class CoinGeckoSource(HttpClient http) : IPriceSource, IInstrumentSearch
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Crypto];
    private HashSet<string>? _vsCurrencies;

    public string Id => "coingecko";
    private static ProviderConfig Cfg => Providers.Get("coingecko");
    public string DisplayName => Cfg.Name;
    public string IconUri => Cfg.IconUri;
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    public Uri PageUrl(LineConfig line) => Cfg.PageUrl(line, ("coinId", CoinId(line)));

    /// <summary>CoinGecko ids for common symbols, for lines that arrived without one (cross-rate legs, migrated settings).</summary>
    private static readonly Dictionary<string, string> KnownIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin", ["ETH"] = "ethereum", ["XRP"] = "ripple", ["DOGE"] = "dogecoin", ["SOL"] = "solana",
        ["ADA"] = "cardano", ["USDT"] = "tether", ["USDC"] = "usd-coin", ["BNB"] = "binancecoin", ["LTC"] = "litecoin",
        ["DOT"] = "polkadot", ["LINK"] = "chainlink", ["AVAX"] = "avalanche-2", ["BCH"] = "bitcoin-cash", ["XLM"] = "stellar",
        ["TRX"] = "tron", ["SHIB"] = "shiba-inu", ["TON"] = "the-open-network", ["ATOM"] = "cosmos", ["XMR"] = "monero",
    };

    public static string CoinId(LineConfig line) =>
        line.CoinId ?? (KnownIds.TryGetValue(line.Symbol, out var id) ? id : line.Symbol.ToLowerInvariant());

    private async Task<string> VsAsync(LineConfig line, CancellationToken ct)
    {
        var vs = line.EffectiveQuote.ToLowerInvariant();
        var supported = await GetVsCurrenciesAsync(ct);
        if (supported is not null && !supported.Contains(vs))
            throw new PairNotSupportedException($"CoinGecko cannot quote in {line.EffectiveQuote}");
        return vs;
    }

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var id = CoinId(line);
        var vs = await VsAsync(line, ct);
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("simplePrice", ("id", id), ("vs", vs)), ct)
            ?? throw new InvalidOperationException("CoinGecko returned no body");

        if (!doc.RootElement.TryGetProperty(id, out var coin) || !coin.TryGetProperty(vs, out var p))
            throw new InvalidOperationException($"CoinGecko: no {line.PairLabel} price for '{id}'");
        var price = p.GetDecimal();
        double? change = coin.TryGetProperty(vs + "_24h_change", out var c) ? Json.Pct(c) : null;
        return new PriceQuote(Id, line.Symbol, price, change, DateTimeOffset.UtcNow, line.EffectiveQuote);
    }

    public async Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct)
    {
        if (line.Kind != InstrumentKind.Crypto) return false;
        if (line.CoinId is null && !KnownIds.ContainsKey(line.Symbol)) return false;
        var supported = await GetVsCurrenciesAsync(ct);
        return supported?.Contains(line.EffectiveQuote.ToLowerInvariant());
    }

    private async Task<HashSet<string>?> GetVsCurrenciesAsync(CancellationToken ct)
    {
        if (_vsCurrencies is not null) return _vsCurrencies;
        try
        {
            var list = await http.GetFromJsonAsync<string[]>(Cfg.ApiUrl("supportedVsCurrencies"), ct);
            return list is null ? null : _vsCurrencies = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    // ---- Search ----

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("search", ("query", query)), ct);
        if (doc is null) return [];

        var list = new List<SearchResult>();
        foreach (var c in doc.RootElement.GetProperty("coins").EnumerateArray().Take(15))
        {
            list.Add(new SearchResult(
                InstrumentKind.Crypto,
                Json.Str(c, "name") ?? "",
                (Json.Str(c, "symbol") ?? "").ToUpperInvariant(),
                Json.Str(c, "id"),
                c.TryGetProperty("market_cap_rank", out var r) && r.ValueKind == JsonValueKind.Number ? $"#{r.GetInt32()}" : null,
                Json.Str(c, "thumb")));
        }
        return list;
    }

    public async Task<IReadOnlyList<SearchResult>> PopularAsync(CancellationToken ct)
    {
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            Cfg.ApiUrl("markets"), ct);
        if (doc is null) return [];

        var list = new List<SearchResult>();
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var image = Json.Str(c, "image")?.Replace("/large/", "/thumb/");
            list.Add(new SearchResult(
                InstrumentKind.Crypto,
                Json.Str(c, "name") ?? "",
                (Json.Str(c, "symbol") ?? "").ToUpperInvariant(),
                Json.Str(c, "id"),
                c.TryGetProperty("market_cap_rank", out var r) && r.ValueKind == JsonValueKind.Number ? $"#{r.GetInt32()}" : null,
                image));
        }
        return list;
    }
}
