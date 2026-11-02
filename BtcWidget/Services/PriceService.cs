using System.Net.Http;
using System.Net.Http.Headers;
using BtcWidget.Models;

namespace BtcWidget.Services;

public sealed record PriceUpdate(Guid LineId, IPriceSource Source, PriceQuote Quote, PriceAnalysis Analysis);

/// <summary>
/// Owns the HTTP client and every provider. Polls each configured line on a timer, persists readings,
/// derives prices the provider has no market for (BTC/DOGE, a stock in CAD), and fans instrument
/// searches out to the providers that support them.
/// </summary>
public sealed class PriceService : IDisposable
{
    private readonly HttpClient _http;
    private readonly HistoryStore _history;
    private readonly Dictionary<string, IPriceSource> _sources;
    private readonly List<IInstrumentSearch> _searchers;
    private readonly YahooSource _yahoo;
    private readonly Dictionary<string, (decimal Rate, DateTimeOffset At)> _fxCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _fxGate = new();
    private volatile List<LineConfig> _lines = [];
    private CancellationTokenSource? _loopCts;
    private TimeSpan _interval;
    private TimeSpan _alertInterval = TimeSpan.FromMinutes(5);
    private readonly Dictionary<Guid, DateTimeOffset> _lastPolled = new();
    private readonly SemaphoreSlim _wake = new(0, 1);

    public event Action<PriceUpdate>? Updated;
    public event Action<Guid, IPriceSource?, Exception>? Failed;

    public IReadOnlyDictionary<string, IPriceSource> Sources => _sources;

    /// <summary>Quote assets offered in the dialogs (currencies.json). Anything else can still be typed as "BTC/XYZ".</summary>
    public static IReadOnlyList<string> CommonQuotes => Currencies.Quotes;

    public PriceService(HistoryStore history, TimeSpan interval)
    {
        _history = history;
        _interval = interval;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // Yahoo and Nasdaq answer a browser-looking agent; the others don't care.
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) BtcWidget/1.1");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _yahoo = new YahooSource(_http);
        IPriceSource[] all =
        [
            new KrakenSource(_http),
            new CoinGeckoSource(_http),
            new CoinbaseSource(_http),
            new BinanceSource(_http),
            _yahoo,
            new NasdaqSource(_http),
        ];
        _sources = all.ToDictionary(s => s.Id);
        _searchers = all.OfType<IInstrumentSearch>().ToList();
    }

    public IEnumerable<IPriceSource> ProvidersFor(InstrumentKind kind) =>
        _sources.Values.Where(s => s.Kinds.Contains(kind));

    // ---- Polling ----

    public void SetLines(IEnumerable<LineConfig> lines) => _lines = lines.Where(l => l.Enabled).ToList();

    public void Start()
    {
        Stop();
        _loopCts = new CancellationTokenSource();
        var ct = _loopCts.Token;
        _ = Task.Run(() => LoopAsync(ct), ct);
    }

    public void Stop()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
    }

    public void SetInterval(TimeSpan interval) { _interval = interval; Nudge(); }

    /// <summary>Poll interval for lines that have an enabled alert rule.</summary>
    public void SetAlertInterval(TimeSpan interval) { _alertInterval = interval; Nudge(); }

    /// <summary>A line polls faster while it has an enabled alert: a crossing an hour late is not much of an alert.</summary>
    public static bool IsWatched(LineConfig line) => line.Alerts.Any(a => a.Enabled);

    public TimeSpan IntervalFor(LineConfig line) => IsWatched(line) ? TimeSpan.FromTicks(Math.Min(_alertInterval.Ticks, _interval.Ticks)) : _interval;

    /// <summary>When the next poll of any line is due (UTC); null until the first poll.</summary>
    public DateTimeOffset? NextDue
    {
        get
        {
            lock (_lastPolled)
            {
                DateTimeOffset? next = null;
                foreach (var line in _lines)
                {
                    if (!_lastPolled.TryGetValue(line.Id, out var last)) return DateTimeOffset.UtcNow;
                    var due = last + IntervalFor(line);
                    if (next is null || due < next) next = due;
                }
                return next;
            }
        }
    }

    /// <summary>Wake the loop so a changed interval or a new alert rule takes effect now rather than after the current wait.</summary>
    public void Nudge()
    {
        if (_wake.CurrentCount == 0) { try { _wake.Release(); } catch (SemaphoreFullException) { } }
    }

    public async Task RefreshNowAsync(CancellationToken ct = default)
    {
        foreach (var line in _lines) await PollLineAsync(line, ct);
    }

    /// <summary>
    /// Each line is polled when its own interval has passed since its last poll (regular lines hourly by
    /// default, lines with alerts every few minutes). Wakes at the earliest due time, at most every minute,
    /// or early on <see cref="Nudge"/>.
    /// </summary>
    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var line in _lines)
            {
                if (ct.IsCancellationRequested) return;
                DateTimeOffset last;
                lock (_lastPolled) _lastPolled.TryGetValue(line.Id, out last);
                if (last + IntervalFor(line) <= DateTimeOffset.UtcNow.AddSeconds(1)) await PollLineAsync(line, ct);
            }
            var next = NextDue ?? DateTimeOffset.UtcNow + _interval;
            var wait = next - DateTimeOffset.UtcNow;
            if (wait < TimeSpan.FromSeconds(1)) wait = TimeSpan.FromSeconds(1);
            if (wait > TimeSpan.FromMinutes(1)) wait = TimeSpan.FromMinutes(1);
            try { await _wake.WaitAsync(wait, ct); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Fetches the quote (native or derived), backfills history if it is thin, then raises <see cref="Updated"/>.</summary>
    public async Task PollLineAsync(LineConfig line, CancellationToken ct = default)
    {
        if (!_sources.TryGetValue(line.ProviderId, out var src))
        {
            Failed?.Invoke(line.Id, null, new InvalidOperationException($"Unknown provider '{line.ProviderId}'"));
            return;
        }

        lock (_lastPolled) _lastPolled[line.Id] = DateTimeOffset.UtcNow;
        PriceQuote q;
        string nativeCurrency;
        try
        {
            (q, nativeCurrency) = await QuoteAsync(src, line, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Failed?.Invoke(line.Id, src, ex);
            return;
        }

        try
        {
            var oldest = _history.OldestTimestamp(src.Id, line.HistoryKey);
            if (oldest is null || oldest > DateTimeOffset.UtcNow.AddDays(-29))
            {
                var pts = await HistoryAsync(src, line, nativeCurrency, ct);
                if (pts.Count > 0) _history.InsertMany(pts);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // History is a nice-to-have; keep going for the live quote.
            System.Diagnostics.Debug.WriteLine($"{src.Id} history: {ex.Message}");
        }

        _history.Insert(new PricePoint(src.Id, line.HistoryKey, q.FetchedAt, q.Price));
        var analysis = PriceAnalysis.Compute(_history, src.Id, line.HistoryKey, q.Price, q.FetchedAt, q.ChangePercent, line.IsDaily);
        Updated?.Invoke(new PriceUpdate(line.Id, src, q, analysis));
    }

    // ---- Conversion rates: where a derived price gets its second leg ----

    /// <summary>Providers that can supply a crypto asset's USD price for a conversion, for the Settings dropdowns.</summary>
    public IEnumerable<IPriceSource> RateProviders => _sources.Values.Where(s => s.Kinds.Contains(InstrumentKind.Crypto));

    /// <summary>Provider asked first for a crypto asset's USD price when a line's price has to be derived.</summary>
    public string RateProvider { get; set; } = "kraken";
    /// <summary>Provider asked when <see cref="RateProvider"/> fails or lacks the asset.</summary>
    public string RateFallback { get; set; } = "coingecko";

    private IEnumerable<IPriceSource> RateSources()
    {
        foreach (var id in new[] { RateProvider, RateFallback }.Distinct(StringComparer.OrdinalIgnoreCase))
            if (_sources.TryGetValue(id, out var s) && s.Kinds.Contains(InstrumentKind.Crypto)) yield return s;
    }

    // ---- Quotes: native pair, else derived ----

    /// <summary>
    /// The price in the line's quote. The provider's own market for the pair when it has one; otherwise its
    /// USD (or listing-currency) price times a conversion rate: Yahoo FX between fiat currencies, the rate
    /// provider's USD market for a crypto asset (so Tesla in BTC = TSLA/USD ÷ BTC/USD). Returns the provider's
    /// native currency too, for history.
    /// </summary>
    private async Task<(PriceQuote Quote, string NativeCurrency)> QuoteAsync(IPriceSource src, LineConfig line, CancellationToken ct)
    {
        var want = line.EffectiveQuote;
        PriceQuote q;
        try
        {
            q = await src.GetQuoteAsync(line, ct);
        }
        catch (PairNotSupportedException) when (line.Kind == InstrumentKind.Crypto && want != "USD")
        {
            q = await src.GetQuoteAsync(line.WithQuote("USD"), ct);
        }
        var native = q.Currency;

        if (want.Length > 0 && !q.Currency.Equals(want, StringComparison.OrdinalIgnoreCase))
        {
            var rate = await RateAsync(q.Currency, want, ct);
            // A crypto quote asset moves as much as the base does, so the change shown is the ratio's.
            // A day's FX move is noise next to the asset's, so a fiat conversion keeps the provider's figure.
            var change = q.ChangePercent;
            if (!PriceFormat.IsFiat(want) && q.ChangePercent is { } a && rate.QuoteChangePercent is { } b)
                change = ((1 + a / 100) / (1 + b / 100) - 1) * 100;
            q = q with
            {
                Price = q.Price * rate.Value, ChangePercent = change, Currency = want,
                Via = $"{line.Symbol} in {q.Currency} {string.Join(" ", rate.Steps)}"
            };
        }
        return (q, native);
    }

    /// <summary>A conversion multiplier with the legs it was built from, for the row tooltip.</summary>
    private sealed record Rate(decimal Value, IReadOnlyList<string> Steps, double? QuoteChangePercent);

    private sealed record Leg(decimal UsdPrice, double? ChangePercent, string Label, bool IsCrypto);

    /// <summary>Multiplier that turns a price in <paramref name="from"/> into one in <paramref name="to"/>.</summary>
    private async Task<Rate> RateAsync(string from, string to, CancellationToken ct)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return new(1m, [], null);
        if (PriceFormat.IsFiat(from) && PriceFormat.IsFiat(to))
        {
            var fx = await FxRateAsync(from, to, ct);
            return new(fx, [$"× {from}/{to} {fx:0.####} (Yahoo FX)"], null);
        }
        // A crypto asset on at least one side: value both sides in USD and divide.
        var num = await UsdLegAsync(from, ct);
        var den = await UsdLegAsync(to, ct);
        if (den.UsdPrice <= 0) throw new InvalidOperationException($"No USD price for {to}");
        var steps = new List<string>(2);
        if (num.Label.Length > 0) steps.Add("× " + num.Label);
        if (den.Label.Length > 0) steps.Add("÷ " + den.Label);
        return new(num.UsdPrice / den.UsdPrice, steps, den.IsCrypto ? den.ChangePercent : null);
    }

    /// <summary>USD value of one unit of a currency or crypto asset. USD itself is 1 and contributes no step.</summary>
    private async Task<Leg> UsdLegAsync(string code, CancellationToken ct)
    {
        if (code.Equals("USD", StringComparison.OrdinalIgnoreCase)) return new(1m, null, "", false);
        if (PriceFormat.IsFiat(code))
        {
            var fx = await FxRateAsync(code, "USD", ct);
            return new(fx, null, $"{code}/USD {fx:0.####} (Yahoo FX)", false);
        }
        var (q, src) = await CryptoUsdAsync(code, ct);
        return new(q.Price, q.ChangePercent, $"{code}/USD {PriceFormat.Price(q.Price)} ({src.DisplayName})", true);
    }

    private readonly Dictionary<string, (PriceQuote Quote, IPriceSource Src, DateTimeOffset At)> _legCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A crypto asset's USD price from the rate provider, then the fallback. Cached for ten minutes.</summary>
    private async Task<(PriceQuote Quote, IPriceSource Src)> CryptoUsdAsync(string asset, CancellationToken ct)
    {
        lock (_fxGate)
        {
            if (_legCache.TryGetValue(asset, out var hit) && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromMinutes(10))
                return (hit.Quote, hit.Src);
        }
        Exception? last = null;
        foreach (var src in RateSources())
        {
            var leg = new LineConfig { Kind = InstrumentKind.Crypto, Symbol = asset, Name = asset, ProviderId = src.Id };
            try
            {
                var q = await src.GetQuoteAsync(leg, ct);
                if (q.Price <= 0) continue;
                lock (_fxGate) _legCache[asset] = (q, src, DateTimeOffset.UtcNow);
                return (q, src);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { last = ex; }
        }
        var tried = string.Join(" or ", RateSources().Select(s => s.DisplayName));
        throw new InvalidOperationException($"No USD price for {asset} from {tried}" + (last is null ? "" : $": {last.Message}"));
    }

    private async Task<decimal> FxRateAsync(string from, string to, CancellationToken ct)
    {
        var key = from + "/" + to;
        lock (_fxGate)
        {
            if (_fxCache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromMinutes(10))
                return hit.Rate;
        }
        var rate = await _yahoo.FxRateAsync(from, to, ct);
        lock (_fxGate) _fxCache[key] = (rate, DateTimeOffset.UtcNow);
        return rate;
    }

    // ---- History: the same two paths as the quote ----

    private async Task<IReadOnlyList<PricePoint>> HistoryAsync(IPriceSource src, LineConfig line, string nativeCurrency, CancellationToken ct)
    {
        var want = line.EffectiveQuote;
        IReadOnlyList<PricePoint> pts;
        var currency = nativeCurrency;
        try
        {
            pts = await src.GetHistoryAsync(line, ct);
            if (line.Kind == InstrumentKind.Crypto) currency = want;   // a native pair is already in the quote asset
        }
        catch (PairNotSupportedException) when (line.Kind == InstrumentKind.Crypto && want != "USD")
        {
            pts = await src.GetHistoryAsync(line.WithQuote("USD"), ct);
            currency = "USD";
        }

        if (want.Length > 0 && !currency.Equals(want, StringComparison.OrdinalIgnoreCase))
        {
            var rates = await RateHistoryAsync(currency, want, ct);
            pts = Combine(pts, rates, divide: false, TimeSpan.FromDays(4));
        }
        // Everything is stored under this provider and the line's own key, whatever legs it came from.
        return pts.Select(p => p.Source == src.Id && p.Symbol == line.HistoryKey ? p : p with { Source = src.Id, Symbol = line.HistoryKey }).ToList();
    }

    /// <summary>Series of the <paramref name="from"/>→<paramref name="to"/> multiplier over the last months.</summary>
    private async Task<IReadOnlyList<PricePoint>> RateHistoryAsync(string from, string to, CancellationToken ct)
    {
        if (PriceFormat.IsFiat(from) && PriceFormat.IsFiat(to)) return await _yahoo.FxHistoryAsync(from, to, ct);
        var num = await UsdHistoryAsync(from, ct);
        var den = await UsdHistoryAsync(to, ct);
        if (den is null) return num ?? [];
        if (num is null) return den.Where(p => p.Price > 0).Select(p => p with { Price = 1m / p.Price }).ToList();
        return Combine(num, den, divide: true, TimeSpan.FromDays(4));
    }

    /// <summary>USD value history of a currency or asset; null for USD itself (a constant 1).</summary>
    private async Task<IReadOnlyList<PricePoint>?> UsdHistoryAsync(string code, CancellationToken ct)
    {
        if (code.Equals("USD", StringComparison.OrdinalIgnoreCase)) return null;
        if (PriceFormat.IsFiat(code)) return await _yahoo.FxHistoryAsync(code, "USD", ct);
        foreach (var src in RateSources())
        {
            var leg = new LineConfig { Kind = InstrumentKind.Crypto, Symbol = code, Name = code, ProviderId = src.Id };
            try
            {
                var pts = await src.GetHistoryAsync(leg, ct);
                if (pts.Count > 0) return pts;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { System.Diagnostics.Debug.WriteLine($"{src.Id} {code}/USD history: {ex.Message}"); }
        }
        return [];
    }

    /// <summary>
    /// Pointwise ratio or product of two series with different timestamps: for each base point take the
    /// nearest conversion point no further than <paramref name="tolerance"/> away (so hourly candles can be
    /// scaled by daily FX rates). Points with no partner are dropped.
    /// </summary>
    private static List<PricePoint> Combine(IReadOnlyList<PricePoint> basePts, IReadOnlyList<PricePoint> conv, bool divide, TimeSpan tolerance)
    {
        if (conv.Count == 0) return [];
        var c = conv.OrderBy(p => p.Timestamp).ToList();
        var ticks = c.Select(p => p.Timestamp.UtcTicks).ToArray();
        var result = new List<PricePoint>(basePts.Count);
        foreach (var b in basePts)
        {
            var i = Array.BinarySearch(ticks, b.Timestamp.UtcTicks);
            if (i < 0) i = ~i;                                       // first element later than b
            var best = -1;
            var bestDist = long.MaxValue;
            foreach (var j in new[] { i - 1, i })
            {
                if (j < 0 || j >= ticks.Length) continue;
                var d = Math.Abs(ticks[j] - b.Timestamp.UtcTicks);
                if (d < bestDist) { bestDist = d; best = j; }
            }
            if (best < 0 || bestDist > tolerance.Ticks) continue;
            var rate = c[best].Price;
            if (rate <= 0) continue;
            result.Add(b with { Price = divide ? b.Price / rate : b.Price * rate });
        }
        return result;
    }

    // ---- Search ----

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var (baseQuery, quote) = ParsePair(query);
        var q = quote is null ? query : baseQuery;

        var tasks = _searchers.Select(async s =>
        {
            try { return await s.SearchAsync(q, ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { return []; }
        });
        var all = await Task.WhenAll(tasks);
        var merged = Merge(all);

        if (quote is not null)
        {
            // "BTC/CAD": offer the coins whose ticker is the base, in that quote. Fall back to a plain search.
            var exact = merged.Where(r => r.Kind == InstrumentKind.Crypto && r.Symbol.Equals(baseQuery, StringComparison.OrdinalIgnoreCase))
                              .Select(r => r with { Quote = quote }).ToList();
            if (exact.Count > 0) return exact;
        }
        return merged.OrderByDescending(r => Score(r, q)).ToList();
    }

    /// <summary>"BTC/CAD", "BTC-CAD", "btc cad", "BTCCAD", "ETHBTC" → (BTC, CAD). Anything else → (query, null).</summary>
    public static (string Base, string? Quote) ParsePair(string query)
    {
        var s = query.Trim().ToUpperInvariant();
        var parts = s.Split(['/', '-', ' ', ':'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts.All(p => p.Length is >= 2 and <= 6 && p.All(char.IsLetter)) && IsQuoteAsset(parts[1]))
            return (parts[0], parts[1]);
        if (parts.Length == 1 && s.Length is >= 5 and <= 10 && s.All(char.IsLetter))
        {
            foreach (var code in QuoteAssets.OrderByDescending(c => c.Length))
                if (s.EndsWith(code, StringComparison.Ordinal) && s.Length - code.Length >= 2)
                    return (s[..^code.Length], code);
        }
        return (query.Trim(), null);
    }

    private static readonly string[] QuoteAssets =
        ["USDT", "USDC", "USD", "CAD", "EUR", "GBP", "AUD", "JPY", "CHF", "KRW", "BTC", "ETH", "SOL", "XRP", "BNB", "DOGE", "LTC", "ADA"];

    private static bool IsQuoteAsset(string code) => QuoteAssets.Contains(code) || PriceFormat.IsFiat(code);

    public async Task<IReadOnlyList<SearchResult>> PopularAsync(CancellationToken ct)
    {
        var tasks = _searchers.Select(async s =>
        {
            try { return await s.PopularAsync(ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { return []; }
        });
        var all = await Task.WhenAll(tasks);
        return Merge(all);
    }

    private static List<SearchResult> Merge(IEnumerable<IReadOnlyList<SearchResult>> groups)
    {
        var seen = new HashSet<(InstrumentKind, string)>();
        var list = new List<SearchResult>();
        foreach (var r in groups.SelectMany(g => g))
            if (seen.Add((r.Kind, r.Symbol))) list.Add(r);
        return list;
    }

    /// <summary>
    /// Relevance for a merged result list. Exact ticker or name wins (a Toronto ticker like SHOP.TO counts as
    /// exact for "shop"); then prefix matches; then real stocks and top coins over the long tail of tokens that
    /// borrow a famous name ("Tesla xStock"). Toronto listings get a nudge: this widget lives in Canada.
    /// </summary>
    private static int Score(SearchResult r, string query)
    {
        var q = query.Trim();
        var cmp = StringComparison.OrdinalIgnoreCase;
        var score = 0;
        var root = r.Symbol.Split('.')[0];
        if (r.Symbol.Equals(q, cmp) || root.Equals(q, cmp)) score += 100;
        else if (r.Symbol.StartsWith(q, cmp)) score += 40;
        if (r.Name.Equals(q, cmp)) score += 90;
        else if (r.Name.StartsWith(q, cmp)) score += 40;
        else if (r.Name.Contains(q, cmp)) score += 10;

        if (r.Kind == InstrumentKind.Crypto)
        {
            // Exchange carries "#rank" for CoinGecko hits.
            var rank = r.Exchange is { Length: > 1 } e && e[0] == '#' && int.TryParse(e[1..], out var n) ? n : int.MaxValue;
            score += rank <= 50 ? 30 : rank <= 300 ? 0 : -25;
        }
        else
        {
            score += r.Kind == InstrumentKind.Stock ? 20 : 10;
            if (IsCanadian(r.Exchange)) score += 15;
        }
        return score;
    }

    private static bool IsCanadian(string? exchange) =>
        exchange is not null && (exchange.Contains("Toronto", StringComparison.OrdinalIgnoreCase) ||
                                 exchange.Contains("TSX", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Default provider for a new line. Crypto: an exchange with the exact pair (Kraken, then Coinbase), else
    /// CoinGecko if it quotes in that currency, else Kraken deriving through USD, else CoinGecko deriving.
    /// Everything else: Yahoo.
    /// </summary>
    public async Task<string> PickProviderAsync(SearchResult result, CancellationToken ct)
    {
        if (result.Kind != InstrumentKind.Crypto) return "yahoo";

        var probe = result.ToLine("");
        foreach (var id in new[] { "kraken", "coinbase" })
            if (await Supports(id, probe)) return id;
        if (await Supports("coingecko", probe)) return "coingecko";
        if (await Supports("kraken", probe.WithQuote("USD"))) return "kraken";
        return "coingecko";

        async Task<bool> Supports(string id, LineConfig line)
        {
            if (!_sources.TryGetValue(id, out var src)) return false;
            try { return await src.SupportsAsync(line, ct) == true; }
            catch { return false; }
        }
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }
}
