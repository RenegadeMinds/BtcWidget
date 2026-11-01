using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>
/// What the stored history says about the current price.
/// <list type="bullet">
/// <item><see cref="Change1"/>/<see cref="Change7d"/>/<see cref="Change30d"/>: percent changes. For crypto 24h / 7d / 30d;
/// for daily instruments 1d / 1w / 1m, where 1d is what the provider reports (vs previous close) because weekends have no bars.</item>
/// <item><see cref="High"/>/<see cref="Low"/>: extremes over the last <see cref="RangeDays"/> days including the current reading;
/// <see cref="FromHigh"/> (≤ 0) and <see cref="FromLow"/> (≥ 0) are the percent distance from them.</item>
/// <item><see cref="IsNewHigh"/>/<see cref="IsNewLow"/>: the current reading beats every stored one in the window.</item>
/// <item><see cref="Series"/>: <see cref="SeriesLength"/> evenly spaced buckets over the last 30 days plus the current price,
/// NaN where there is no data yet, for the sparkline.</item>
/// </list>
/// </summary>
public sealed record PriceAnalysis(
    double? Change1,
    double? Change7d,
    double? Change30d,
    decimal? High,
    decimal? Low,
    double? FromHigh,
    double? FromLow,
    int RangeDays,
    bool IsNewHigh,
    bool IsNewLow,
    IReadOnlyList<double> Series)
{
    public const int SeriesLength = 60;
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);
    public static readonly PriceAnalysis Empty = new(null, null, null, null, null, null, null, 0, false, false, []);

    public static PriceAnalysis Compute(HistoryStore history, string source, string symbol, decimal current,
        DateTimeOffset at, double? providerChange, bool daily)
    {
        var now = DateTimeOffset.UtcNow;
        double? c1, c7, c30;
        if (daily)
        {
            c1 = providerChange ?? Pct(current, history.PriceAt(source, symbol, now.AddDays(-1), TimeSpan.FromDays(3)));
            c7 = Pct(current, history.PriceAt(source, symbol, now.AddDays(-7), TimeSpan.FromDays(4)));
            c30 = Pct(current, history.PriceAt(source, symbol, now.AddDays(-30), TimeSpan.FromDays(6)));
        }
        else
        {
            c1 = Pct(current, history.PriceAt(source, symbol, now.AddDays(-1), TimeSpan.FromHours(3))) ?? providerChange;
            c7 = Pct(current, history.PriceAt(source, symbol, now.AddDays(-7), TimeSpan.FromHours(12)));
            c30 = Pct(current, history.PriceAt(source, symbol, now.AddDays(-30), TimeSpan.FromDays(2)));
        }

        // One read covers range, extremes and the sparkline. Rows are ordered by timestamp.
        var pts = history.Recent(source, symbol, Window);

        decimal? high = null, low = null;
        var rangeDays = 0;
        bool newHigh = false, newLow = false;
        decimal priorHigh = decimal.MinValue, priorLow = decimal.MaxValue;
        DateTimeOffset? oldest = null;
        foreach (var p in pts)
        {
            if (p.Timestamp >= at) continue;          // the reading we are analysing
            oldest ??= p.Timestamp;
            if (p.Price > priorHigh) priorHigh = p.Price;
            if (p.Price < priorLow) priorLow = p.Price;
        }
        if (oldest is { } o)
        {
            rangeDays = Math.Clamp((int)Math.Round((now - o).TotalDays), 1, 30);
            newHigh = current > priorHigh;
            newLow = current < priorLow;
            high = Math.Max(priorHigh, current);
            low = Math.Min(priorLow, current);
        }

        return new PriceAnalysis(c1, c7, c30, high, low, Pct(current, high), Pct(current, low), rangeDays, newHigh, newLow,
            Downsample(pts, now - Window, now, current));
    }

    /// <summary>
    /// Buckets readings into <see cref="SeriesLength"/> equal time slices (mean per slice, carried forward across
    /// gaps such as weekends) and appends the current price. Leading slices with no data stay NaN so a line with
    /// two days of history draws as a short stub on the right rather than stretching across the whole width.
    /// </summary>
    private static IReadOnlyList<double> Downsample(IReadOnlyList<PricePoint> pts, DateTimeOffset from, DateTimeOffset to, decimal current)
    {
        if (pts.Count < 2) return [];
        var sums = new double[SeriesLength];
        var counts = new int[SeriesLength];
        var span = (to - from).TotalSeconds;
        foreach (var p in pts)
        {
            var i = (int)((p.Timestamp - from).TotalSeconds / span * SeriesLength);
            i = Math.Clamp(i, 0, SeriesLength - 1);
            sums[i] += (double)p.Price;
            counts[i]++;
        }
        var result = new double[SeriesLength + 1];
        var last = double.NaN;
        for (var i = 0; i < SeriesLength; i++)
        {
            if (counts[i] > 0) last = sums[i] / counts[i];
            result[i] = last;
        }
        result[SeriesLength] = (double)current;
        return result;
    }

    private static double? Pct(decimal current, decimal? past) =>
        past is > 0 ? (double)((current - past.Value) / past.Value * 100m) : null;
}
