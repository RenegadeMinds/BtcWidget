using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>A place prices come from: an exchange, an aggregator, a quote API.</summary>
public interface IPriceSource
{
    /// <summary>Stable id used in settings and the history table, e.g. "kraken".</summary>
    string Id { get; }
    string DisplayName { get; }
    /// <summary>pack:// URI of the small icon shown for this provider.</summary>
    string IconUri { get; }
    /// <summary>Instrument kinds this provider can quote.</summary>
    IReadOnlySet<InstrumentKind> Kinds { get; }

    /// <summary>Web page for the instrument on this provider.</summary>
    Uri PageUrl(LineConfig line);

    /// <summary>
    /// Price in the line's quote currency (<see cref="LineConfig.EffectiveQuote"/>). Throws
    /// <see cref="PairNotSupportedException"/> when the provider has no such market; <see cref="PriceService"/>
    /// then derives the price through USD.
    /// </summary>
    Task<PriceQuote> GetQuoteAsync(LineConfig line, CancellationToken ct);

    /// <summary>Optional: historical closes for backfilling analysis on first run, keyed by <see cref="LineConfig.HistoryKey"/>.</summary>
    Task<IReadOnlyList<PricePoint>> GetHistoryAsync(LineConfig line, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PricePoint>>([]);

    /// <summary>Whether the provider is known to carry this instrument in this quote. Null means "can't tell cheaply".</summary>
    Task<bool?> SupportsAsync(LineConfig line, CancellationToken ct)
        => Task.FromResult<bool?>(null);
}

/// <summary>Providers that can also find instruments by name.</summary>
public interface IInstrumentSearch
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct);
    /// <summary>Shown in the Add pane when the search box is empty.</summary>
    Task<IReadOnlyList<SearchResult>> PopularAsync(CancellationToken ct);
}

/// <summary>The provider exists and answers, but has no market for this base/quote pair.</summary>
public sealed class PairNotSupportedException(string message) : Exception(message);
