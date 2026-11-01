using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BtcWidget.Models;

namespace BtcWidget.Services;

public sealed class KrakenSource(HttpClient http) : IPriceSource
{
    private static readonly HashSet<InstrumentKind> _kinds = [InstrumentKind.Crypto];
    private HashSet<string>? _pairs;

    public string Id => "kraken";
    public string DisplayName => "Kraken";
    public string IconUri => "pack://application:,,,/Assets/kraken.png";
    public IReadOnlySet<InstrumentKind> Kinds => _kinds;

    public Uri PageUrl(LineConfig line) => new($"https://www.kraken.com/prices/{line.Symbol.ToLowerInvariant()}");

    /// <summary>Kraken still calls Bitcoin XBT and Doge XDG.</summary>
    private static string Asset(string symbol) => symbol.ToUpperInvariant() switch
    {
        "BTC" => "XBT",
        "DOGE" => "XDG",
        var s => s
    };

    /// <summary>Kraken's altname for the pair: XBTUSD, XBTCAD, ETHXBT, XDGXBT.</summary>
    private static string Alt(LineConfig line) => Asset(line.Symbol) + Asset(line.EffectiveQuote);

    private async Task<string> PairAsync(LineConfig line, CancellationToken ct)
    {
        var alt = Alt(line);
        var pairs = await GetPairsAsync(ct);
        if (pairs is not null && !pairs.Contains(alt))
            throw new PairNotSupportedException($"Kraken has no {line.PairLabel} market");
        return alt;
    }

    public async Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct)
    {
        var pair = await PairAsync(line, ct);
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            $"https://api.kraken.com/0/public/Ticker?pair={pair}", ct)
            ?? throw new InvalidOperationException("Kraken returned no body");

        ThrowIfError(doc, line);
        var ticker = doc.RootElement.GetProperty("result").EnumerateObject().First().Value;
        var last = Json.Dec(ticker.GetProperty("c")[0]);
        var open = Json.Dec(ticker.GetProperty("o")); // today's open (00:00 UTC); only a fallback until history exists

        double? change = open > 0 ? (double)((last - open) / open * 100m) : null;
        return new PriceQuote(Id, line.Symbol, last, change, DateTimeOffset.UtcNow, line.EffectiveQuote);
    }

    public async Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct)
    {
        var pair = await PairAsync(line, ct);
        // interval=60 returns up to 720 hourly candles (~30 days).
        using var doc = await http.GetFromJsonAsync<JsonDocument>(
            $"https://api.kraken.com/0/public/OHLC?pair={pair}&interval=60", ct)
            ?? throw new InvalidOperationException("Kraken returned no body");

        ThrowIfError(doc, line);
        var result = doc.RootElement.GetProperty("result");
        var candles = result.EnumerateObject().First(p => p.Name != "last").Value;
        var list = new List<PricePoint>(candles.GetArrayLength());
        foreach (var c in candles.EnumerateArray())
        {
            // [time, open, high, low, close, vwap, volume, count]
            var ts = DateTimeOffset.FromUnixTimeSeconds(c[0].GetInt64());
            list.Add(new PricePoint(Id, line.HistoryKey, ts, Json.Dec(c[4])));
        }
        return list;
    }

    public async Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct)
    {
        if (line.Kind != InstrumentKind.Crypto) return false;
        var pairs = await GetPairsAsync(ct);
        return pairs?.Contains(Alt(line));
    }

    private async Task<HashSet<string>?> GetPairsAsync(CancellationToken ct)
    {
        if (_pairs is not null) return _pairs;
        try
        {
            using var doc = await http.GetFromJsonAsync<JsonDocument>("https://api.kraken.com/0/public/AssetPairs", ct);
            if (doc is null) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.GetProperty("result").EnumerateObject())
                if (Json.Str(p.Value, "altname") is { } alt) set.Add(alt);
            return _pairs = set;
        }
        catch
        {
            return null;
        }
    }

    private static void ThrowIfError(JsonDocument doc, LineConfig line)
    {
        if (!doc.RootElement.TryGetProperty("error", out var err) || err.GetArrayLength() == 0) return;
        var msg = err[0].GetString() ?? "error";
        if (msg.Contains("Unknown asset pair", StringComparison.OrdinalIgnoreCase))
            throw new PairNotSupportedException($"Kraken has no {line.PairLabel} market");
        throw new InvalidOperationException("Kraken: " + msg);
    }
}
