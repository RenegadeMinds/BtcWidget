using System.Globalization;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>Evaluates a line's alert rules against a fresh price update.</summary>
public static class AlertEngine
{
    public sealed record Fired(AlertRule Rule, string Message);

    /// <summary>Minimum history before "new 30-day high/low" means anything.</summary>
    public const int MinRangeDaysForExtremes = 7;
    private static readonly TimeSpan ExtremeCooldown = TimeSpan.FromHours(12);

    /// <summary>
    /// Returns the rules that fired. Mutates <see cref="AlertRule.Armed"/> / <see cref="AlertRule.LastFired"/>;
    /// <paramref name="stateChanged"/> tells the caller to persist settings.
    /// </summary>
    public static List<Fired> Evaluate(LineConfig line, PriceUpdate u, out bool stateChanged)
    {
        stateChanged = false;
        var fired = new List<Fired>();
        var price = u.Quote.Price;
        var a = u.Analysis;
        var now = u.Quote.FetchedAt;

        foreach (var r in line.Alerts)
        {
            if (!r.Enabled) continue;
            var holds = r.Type switch
            {
                AlertType.PriceAbove => price >= r.Threshold,
                AlertType.PriceBelow => price <= r.Threshold,
                AlertType.UpMoreThan => a.Change1 is { } c && c >= (double)r.Threshold,
                AlertType.DownMoreThan => a.Change1 is { } c && c <= -(double)r.Threshold,
                AlertType.NewHigh30d => a.IsNewHigh && a.RangeDays >= MinRangeDaysForExtremes,
                AlertType.NewLow30d => a.IsNewLow && a.RangeDays >= MinRangeDaysForExtremes,
                _ => false
            };

            if (r.Type is AlertType.NewHigh30d or AlertType.NewLow30d)
            {
                // A run of new highs would otherwise fire every poll; one notification per half day is plenty.
                if (holds && (r.LastFired is null || now - r.LastFired.Value > ExtremeCooldown))
                {
                    r.LastFired = now;
                    stateChanged = true;
                    fired.Add(new Fired(r, Message(r, line, u)));
                }
                continue;
            }

            if (holds && r.Armed)
            {
                r.Armed = false;
                r.LastFired = now;
                stateChanged = true;
                fired.Add(new Fired(r, Message(r, line, u)));
            }
            else if (!holds && !r.Armed)
            {
                r.Armed = true;
                stateChanged = true;
            }
        }
        return fired;
    }

    /// <summary>
    /// Short rule text for the dialogs: "Price above $110,000", "Up more than 5% (24h)". Pass the currency the
    /// line is currently shown in when known; otherwise the line's quote (USD for a stock with no quote set).
    /// </summary>
    public static string Describe(AlertRule r, LineConfig line, string? currency = null)
    {
        currency ??= line.EffectiveQuote.Length > 0 ? line.EffectiveQuote : "USD";
        var window = line.IsDaily ? "1d" : "24h";
        return r.Type switch
        {
            AlertType.PriceAbove => $"Price above {PriceFormat.Price(r.Threshold, currency, line.Kind)}",
            AlertType.PriceBelow => $"Price below {PriceFormat.Price(r.Threshold, currency, line.Kind)}",
            AlertType.UpMoreThan => $"Up more than {Num(r.Threshold)}% ({window})",
            AlertType.DownMoreThan => $"Down more than {Num(r.Threshold)}% ({window})",
            AlertType.NewHigh30d => "New 30-day high",
            AlertType.NewLow30d => "New 30-day low",
            _ => r.Type.ToString()
        };
    }

    private static string Message(AlertRule r, LineConfig line, PriceUpdate u)
    {
        var p = PriceFormat.Price(u.Quote.Price, u.Quote.Currency, line.Kind);
        var window = line.IsDaily ? "today" : "in 24h";
        return r.Type switch
        {
            AlertType.PriceAbove => $"{line.Name} crossed above {PriceFormat.Price(r.Threshold, u.Quote.Currency, line.Kind)} · now {p}",
            AlertType.PriceBelow => $"{line.Name} fell below {PriceFormat.Price(r.Threshold, u.Quote.Currency, line.Kind)} · now {p}",
            AlertType.UpMoreThan => $"{line.Name} up {PriceFormat.Percent(u.Analysis.Change1)} {window} · {p}",
            AlertType.DownMoreThan => $"{line.Name} down {PriceFormat.Percent(u.Analysis.Change1)} {window} · {p}",
            AlertType.NewHigh30d => $"{line.Name} at a new {u.Analysis.RangeDays}-day high · {p}",
            AlertType.NewLow30d => $"{line.Name} at a new {u.Analysis.RangeDays}-day low · {p}",
            _ => $"{line.Name} {p}"
        };
    }

    private static string Num(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Threshold typed by the user: "110000", "110,000", "$110k", "C$1.2m", "5%", current or invariant culture.</summary>
    public static decimal? ParseThreshold(string input)
    {
        var s = input.Trim().TrimEnd('%').Trim();
        var start = 0;
        while (start < s.Length && !char.IsDigit(s[start]) && s[start] != '.' && s[start] != ',') start++;
        s = s[start..];
        var mult = 1m;
        if (s.EndsWith('k') || s.EndsWith('K')) { mult = 1000m; s = s[..^1]; }
        else if (s.EndsWith('m') || s.EndsWith('M')) { mult = 1_000_000m; s = s[..^1]; }
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ||
            decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
            return v * mult;
        return null;
    }
}
