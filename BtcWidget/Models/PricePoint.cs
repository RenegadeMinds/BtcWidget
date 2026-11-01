namespace BtcWidget.Models;

/// <summary>One stored reading. <see cref="Symbol"/> is the line's <see cref="LineConfig.HistoryKey"/> ("BTC", "BTC/CAD", "SHOP.TO").</summary>
public readonly record struct PricePoint(string Source, string Symbol, DateTimeOffset Timestamp, decimal Price);

/// <summary>
/// Latest reading from one provider. <see cref="ChangePercent"/> is what the provider itself reports:
/// 24h for crypto, since previous close for daily instruments. Used when local history is too thin.
/// <see cref="Via"/> is set when the price was derived rather than read from a market ("BTC/USD ÷ DOGE/USD").
/// </summary>
public sealed record PriceQuote(
    string ProviderId,
    string Symbol,
    decimal Price,
    double? ChangePercent,
    DateTimeOffset FetchedAt,
    string Currency = "USD",
    string? Via = null);

/// <summary>One hit from an instrument search, before a provider is chosen.</summary>
public sealed record SearchResult(
    InstrumentKind Kind,
    string Name,
    string Symbol,
    string? CoinId,
    string? Exchange,
    string? LogoUrl)
{
    /// <summary>Quote asset the user asked for in the query ("BTC/CAD" → "CAD"); empty = default.</summary>
    public string Quote { get; init; } = "";

    public LineConfig ToLine(string providerId) => new()
    {
        Kind = Kind, Name = Name, Symbol = Symbol, CoinId = CoinId, LogoUrl = LogoUrl, ProviderId = providerId,
        Quote = Quote, Exchange = Kind == InstrumentKind.Crypto ? null : Exchange
    };
}
