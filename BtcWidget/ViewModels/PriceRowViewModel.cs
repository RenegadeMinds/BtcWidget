using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

/// <summary>One line in the widget: an instrument from a single provider with its change figures.</summary>
public sealed partial class PriceRowViewModel : ObservableObject
{
    private readonly Uri? _pageUrl;

    public LineConfig Line { get; }
    public Guid Id => Line.Id;
    public string Name => Line.Name;
    public string Symbol => Line.Symbol;
    public string ProviderName { get; }
    public string ProviderIconUri { get; }
    /// <summary>Instrument logo when we have one (CoinGecko thumbs), otherwise the provider's badge.</summary>
    public string LogoUri => Line.LogoUrl ?? ProviderIconUri;
    public string ChangeLabel => Line.IsDaily ? "1d" : "24h";
    public string Label7d => Line.IsDaily ? "1w" : "7d";
    public string Label30d => Line.IsDaily ? "1m" : "30d";

    [ObservableProperty] private decimal? _price;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private double? _change;
    [ObservableProperty] private double? _change7d;
    [ObservableProperty] private double? _change30d;
    [ObservableProperty] private decimal? _high;
    [ObservableProperty] private decimal? _low;
    [ObservableProperty] private double? _fromHigh;
    [ObservableProperty] private double? _fromLow;
    [ObservableProperty] private int _rangeDays;
    [ObservableProperty] private IReadOnlyList<double> _series = [];
    [ObservableProperty] private DateTimeOffset? _updatedAt;
    [ObservableProperty] private string? _error;
    /// <summary>How a derived price was built ("BTC/USD ÷ DOGE/USD on Kraken"); null for a native market.</summary>
    [ObservableProperty] private string? _via;

    /// <summary>True from the moment an alert fires until the row is clicked.</summary>
    [ObservableProperty] private bool _isAlerting;
    [ObservableProperty] private string? _alertText;

    /// <summary>Set by MainViewModel: opens the Alerts dialog for this line (row context menu).</summary>
    public Action<Guid>? AlertsRequested { get; set; }
    /// <summary>Whether any alert rule is set on this line; shows a small bell after the name.</summary>
    public bool HasRules => Line.Alerts.Count > 0;
    public string RulesText => HasRules ? $"{Line.Alerts.Count} alert{(Line.Alerts.Count == 1 ? "" : "s")} set" : "";
    /// <summary>Bell glyph (Segoe Fluent Icons) after the provider label when rules exist, else nothing.</summary>
    public string BellGlyph => HasRules ? "" : "";
    public string OpenHeader => $"Open on {ProviderName}";

    public void AlertsChanged()
    {
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(RulesText));
        OnPropertyChanged(nameof(BellGlyph));
        OnPropertyChanged(nameof(Tooltip));
    }

    [RelayCommand] private void EditAlerts() => AlertsRequested?.Invoke(Id);

    public PriceRowViewModel(LineConfig line, string providerName, string providerIconUri, Uri? pageUrl)
    {
        Line = line;
        ProviderName = providerName;
        ProviderIconUri = providerIconUri;
        _pageUrl = pageUrl;
    }

    public PriceRowViewModel(LineConfig line, IPriceSource provider)
        : this(line, provider.DisplayName, provider.IconUri, provider.PageUrl(line)) { }

    public string PriceText => Price is not { } p ? "—" : PriceFormat.Price(p, Currency, Line.Kind);

    /// <summary>
    /// Muted text after the name: the pair when it is not the plain USD one, the exchange for stocks, the
    /// provider, and "derived" when no market quotes the pair directly. "BTC/CAD · Kraken", "Toronto · Yahoo Finance".
    /// </summary>
    public string ProviderLabel
    {
        get
        {
            var parts = new List<string>(4);
            if (Line.Kind == InstrumentKind.Crypto)
            {
                if (Line.EffectiveQuote != "USD") parts.Add(Line.PairLabel);
            }
            else
            {
                if (!string.IsNullOrEmpty(Line.Exchange)) parts.Add(Line.Exchange);
                if (Line.Quote.Length > 0) parts.Add("in " + Line.EffectiveQuote);
            }
            parts.Add(ProviderName);
            if (Via is not null) parts.Add("derived");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Label for the range figures: "30d" once we have most of the window, else the days we do have.</summary>
    public string RangeLabel => RangeDays >= 28 ? "30d" : $"{RangeDays}d";

    /// <summary>
    /// Where the price sits in its recent range, as one short phrase: "at 30d high", "4.2% below 30d high",
    /// "1.3% above 30d low". Picks whichever extreme is nearer. Null until there are a few days of history.
    /// </summary>
    public string? RangeText
    {
        get
        {
            if (RangeDays < 5 || FromHigh is not { } fh || FromLow is not { } fl) return null;
            if (fh > -0.5) return $"at {RangeLabel} high";
            if (fl < 0.5) return $"at {RangeLabel} low";
            return -fh <= fl ? $"{-fh:0.0}% below {RangeLabel} high" : $"{fl:0.0}% above {RangeLabel} low";
        }
    }

    public string Tooltip
    {
        get
        {
            if (Error is not null) return $"{Name} via {ProviderName}\n{Error}";
            var s = $"{Name} ({Line.PairLabel}) via {ProviderName}\n" +
                    $"{ChangeLabel} {PriceFormat.Percent(Change)} · {Label7d} {PriceFormat.Percent(Change7d)} · {Label30d} {PriceFormat.Percent(Change30d)}";
            if (High is { } hi && Low is { } lo && RangeDays > 0)
                s += $"\n{RangeLabel} high {PriceFormat.Price(hi, Currency, Line.Kind)} ({PriceFormat.Percent(FromHigh)}) · " +
                     $"low {PriceFormat.Price(lo, Currency, Line.Kind)} ({PriceFormat.Percent(FromLow)})";
            if (Via is not null) s += $"\nDerived: {Via}";
            if (AlertText is not null) s += $"\n⚠ {AlertText}";
            if (HasRules) s += $"\n{RulesText}: " + string.Join(", ", Line.Alerts.Select(a => AlertEngine.Describe(a, Line, Currency)));
            s += $"\nUpdated {UpdatedAt?.ToLocalTime():HH:mm} · click to open {ProviderName} · right-click for alerts";
            return s;
        }
    }

    public void Apply(PriceUpdate u)
    {
        Price = u.Quote.Price;
        Currency = u.Quote.Currency;
        Change = u.Analysis.Change1;
        Change7d = u.Analysis.Change7d;
        Change30d = u.Analysis.Change30d;
        High = u.Analysis.High;
        Low = u.Analysis.Low;
        FromHigh = u.Analysis.FromHigh;
        FromLow = u.Analysis.FromLow;
        RangeDays = u.Analysis.RangeDays;
        Series = u.Analysis.Series;
        Via = u.Quote.Via;
        UpdatedAt = u.Quote.FetchedAt;
        Error = null;
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(ProviderLabel));
        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(RangeText));
        OnPropertyChanged(nameof(Tooltip));
    }

    public void Fail(Exception ex)
    {
        Error = ex.Message;
        OnPropertyChanged(nameof(Tooltip));
    }

    public void Alert(string message)
    {
        AlertText = message;
        IsAlerting = true;
        OnPropertyChanged(nameof(Tooltip));
    }

    public void DismissAlert()
    {
        if (!IsAlerting && AlertText is null) return;
        IsAlerting = false;
        AlertText = null;
        OnPropertyChanged(nameof(Tooltip));
    }

    [RelayCommand]
    private void Open()
    {
        DismissAlert();
        if (_pageUrl is not null) Shell.OpenUrl(_pageUrl);
    }

    partial void OnPriceChanged(decimal? value) => OnPropertyChanged(nameof(PriceText));
}
