using System.Text.Json.Serialization;

namespace BtcWidget.Models;

/// <summary>Persisted user settings. Stored as JSON in %LocalAppData%\BtcWidget\settings.json.</summary>
public sealed class AppSettings
{
    public double WindowOpacity { get; set; } = 0.85;
    public double TextOpacity { get; set; } = 1.0;
    public int PollIntervalMinutes { get; set; } = 60;
    /// <summary>Lines that have an enabled alert poll this often instead, so a crossing is caught promptly.</summary>
    public int AlertPollMinutes { get; set; } = 5;
    /// <summary>Minutes after being minimized the widget comes back on its own (without taking focus). 0 = never.</summary>
    public int RestoreAfterMinutes { get; set; } = 0;
    /// <summary>Bring a minimized widget back when an alert fires. Each rule can override this.</summary>
    public bool RestoreOnAlert { get; set; } = true;
    /// <summary>What the title-bar X does: "exit" or "minimize".</summary>
    public string CloseButtonAction { get; set; } = "exit";
    /// <summary>Ask GitHub for the latest release once a day.</summary>
    public bool CheckForUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheck { get; set; }
    /// <summary>Newest release seen on GitHub, and its page, so About can show it without asking again.</summary>
    public string? LatestVersion { get; set; }
    public string? LatestUrl { get; set; }
    public bool AlwaysOnTop { get; set; } = false;
    public bool ShowInTaskbar { get; set; } = true;
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+B";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 300;
    public double FontSize { get; set; } = 13;

    /// <summary>Second line under each price: sparkline, 7d / 30d, distance from the 30-day high or low.</summary>
    public bool ShowDetails { get; set; } = true;

    /// <summary>Maximum height of the list of lines in pixels; the list scrolls beyond it. 0 = grow with content.</summary>
    public double ListHeight { get; set; } = 0;

    /// <summary>Raise a Windows notification when an alert fires (the row is highlighted either way).</summary>
    public bool ToastsEnabled { get; set; } = true;

    /// <summary>Hover tooltips on the widget (row details, button names).</summary>
    public bool ShowTooltips { get; set; } = true;

    /// <summary>
    /// Where a derived price gets its conversion leg: the provider asked first for a crypto asset's USD price
    /// (Tesla in BTC needs BTC/USD; BTC/DOGE needs DOGE/USD), and the one asked when it fails. Fiat rates
    /// always come from Yahoo Finance.
    /// </summary>
    public string RateProvider { get; set; } = "kraken";
    public string RateFallback { get; set; } = "coingecko";

    /// <summary>Lines shown in the widget, in display order. One line = one instrument from one provider.</summary>
    public List<LineConfig> Lines { get; set; } = [];

    // ---- News (headline popup) ----

    /// <summary>Pre-1.3: enabled feed ids and user-added feeds lived here. Folded into feeds.json on load, then dropped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? NewsFeeds { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<NewsFeed>? CustomNewsFeeds { get; set; }
    /// <summary>Headlines shown in the popup, newest first, across all feeds.</summary>
    public int NewsCount { get; set; } = 10;
    public int NewsRefreshMinutes { get; set; } = 15;
    /// <summary>Headlines older than this are dropped, so a stale feed does not linger.</summary>
    public int NewsMaxAgeHours { get; set; } = 48;
    /// <summary>"browser" = the default browser; "reader" = the built-in reader window.</summary>
    public string NewsOpenIn { get; set; } = "browser";
    /// <summary>When the popup was last open; headlines newer than this are shown bright.</summary>
    public DateTimeOffset? NewsSeenAt { get; set; }
    /// <summary>Keep the popup open when it loses focus (the pin in its header).</summary>
    public bool NewsPinned { get; set; } = false;
    /// <summary>Last headline filter: comma-separated words, any of which must appear in the title.</summary>
    public string NewsFilter { get; set; } = "";
    public double NewsWidth { get; set; } = 420;
    public double NewsHeight { get; set; } = 380;
    /// <summary>Ask pages for their dark theme in the built-in reader. Off = pages as the site designed them.</summary>
    public bool ReaderDarkPages { get; set; } = false;
    public double ReaderWidth { get; set; } = 900;
    public double ReaderHeight { get; set; } = 700;

    /// <summary>Pre-1.1 shape. Migrated to <see cref="Lines"/> on load and then dropped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FeedConfig>? Feeds { get; set; }
}

public enum InstrumentKind
{
    Crypto,
    Stock,
    Etf,
    Index,
    Fx,
    Commodity
}

public sealed class LineConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public InstrumentKind Kind { get; set; }
    /// <summary>Human name, e.g. "Bitcoin", "Tesla, Inc.".</summary>
    public string Name { get; set; } = "";
    /// <summary>Canonical symbol: BTC, TSLA, ^GSPC, EURUSD=X. Providers map this to their own id.</summary>
    public string Symbol { get; set; } = "";
    /// <summary>CoinGecko id for crypto (bitcoin, ripple). Used by CoinGecko and for logos/links.</summary>
    public string? CoinId { get; set; }
    public string ProviderId { get; set; } = "";
    public string? LogoUrl { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quote asset / currency the price is shown in: "CAD", "BTC", "DOGE". Empty = the default, which is USD for
    /// crypto and the listing currency for everything else (SHOP.TO shows CAD). When no provider has the pair
    /// the price is derived through USD or an FX rate.
    /// </summary>
    public string Quote { get; set; } = "";

    /// <summary>Exchange label from the search result, for stocks: "Toronto", "NASDAQ", "Korea".</summary>
    public string? Exchange { get; set; }

    /// <summary>Alert rules for this line. Evaluated after every poll.</summary>
    public List<AlertRule> Alerts { get; set; } = [];

    /// <summary>Stocks and the like only have one close per session, so change figures are 1d/1w/1m.</summary>
    [JsonIgnore] public bool IsDaily => Kind != InstrumentKind.Crypto;

    /// <summary>The quote actually used: the explicit one, else USD for crypto, else "" (the listing currency).</summary>
    [JsonIgnore] public string EffectiveQuote =>
        Quote.Length > 0 ? Quote.ToUpperInvariant() : Kind == InstrumentKind.Crypto ? "USD" : "";

    /// <summary>"BTC/CAD", "BTC/USD", "SHOP.TO", "SHOP.TO/USD".</summary>
    [JsonIgnore] public string PairLabel => EffectiveQuote.Length > 0 ? $"{Symbol}/{EffectiveQuote}" : Symbol;

    /// <summary>
    /// Key the readings are stored under. Plain symbol for the default quote (keeps pre-pair history valid),
    /// "BTC/CAD" otherwise.
    /// </summary>
    [JsonIgnore] public string HistoryKey =>
        Quote.Length == 0 || (Kind == InstrumentKind.Crypto && EffectiveQuote == "USD") ? Symbol : $"{Symbol}/{EffectiveQuote}";

    /// <summary>Copy of this line with another quote; used for the legs of a derived price.</summary>
    public LineConfig WithQuote(string quote) => new()
    {
        Id = Id, Kind = Kind, Name = Name, Symbol = Symbol, CoinId = CoinId, ProviderId = ProviderId, LogoUrl = LogoUrl,
        Enabled = Enabled, Quote = quote, Exchange = Exchange, Alerts = Alerts
    };

    /// <summary>The starter lines, from providers.json → defaultLines.</summary>
    public static List<LineConfig> Defaults() => Services.Providers.DefaultLines.Select(d => d.ToLine()).ToList();
}

public enum AlertType
{
    /// <summary>Price crosses up through <see cref="AlertRule.Threshold"/>.</summary>
    PriceAbove,
    /// <summary>Price crosses down through <see cref="AlertRule.Threshold"/>.</summary>
    PriceBelow,
    /// <summary>24h (crypto) or 1d (stocks) change reaches +Threshold percent.</summary>
    UpMoreThan,
    /// <summary>24h / 1d change reaches −Threshold percent.</summary>
    DownMoreThan,
    /// <summary>Current price is above every stored reading of the last 30 days.</summary>
    NewHigh30d,
    /// <summary>Current price is below every stored reading of the last 30 days.</summary>
    NewLow30d
}

public sealed class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AlertType Type { get; set; }
    /// <summary>Price for PriceAbove/PriceBelow, percent for Up/DownMoreThan, unused for the 30d types.</summary>
    public decimal Threshold { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Cleared when the rule fires and set again once the condition no longer holds, so a single
    /// crossing produces a single notification rather than one per poll.
    /// </summary>
    public bool Armed { get; set; } = true;
    public DateTimeOffset? LastFired { get; set; }
    /// <summary>Bring a minimized widget back when this fires: null = follow the Settings default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Restore { get; set; }

    [JsonIgnore] public bool NeedsThreshold => Type is not (AlertType.NewHigh30d or AlertType.NewLow30d);
}

// ---- Legacy (settings written by the first version) ----

public enum FeedType { Price, Rss }

public sealed class FeedConfig
{
    public FeedType Type { get; set; }
    public string Symbol { get; set; } = "BTC";
    public List<string> Sources { get; set; } = [];
    public string? Url { get; set; }
    public bool Enabled { get; set; } = true;
}
