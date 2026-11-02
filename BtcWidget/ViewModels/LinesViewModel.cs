using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

public sealed record ProviderOption(string Id, string DisplayName, string IconUri);

public sealed record KindFilter(string Label, InstrumentKind? Kind);

/// <summary>A quote asset choice. Code "" = the default: USD for crypto, the listing currency for stocks.</summary>
public sealed record QuoteOption(string Label, string Code)
{
    public static readonly IReadOnlyList<QuoteOption> All =
        [new("Default", ""), .. PriceService.CommonQuotes.Select(q => new QuoteOption(q, q))];

    public static QuoteOption For(string code) =>
        All.FirstOrDefault(o => o.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? new QuoteOption(code.ToUpperInvariant(), code.ToUpperInvariant());
}

/// <summary>One entry in the "add alert" type dropdown.</summary>
public sealed record AlertTypeOption(AlertType Type, string Label, bool NeedsValue, string Unit)
{
    public static readonly IReadOnlyList<AlertTypeOption> All =
    [
        new(AlertType.PriceAbove, "Price above", true, "$"),
        new(AlertType.PriceBelow, "Price below", true, "$"),
        new(AlertType.UpMoreThan, "Up more than", true, "%"),
        new(AlertType.DownMoreThan, "Down more than", true, "%"),
        new(AlertType.NewHigh30d, "New 30-day high", false, ""),
        new(AlertType.NewLow30d, "New 30-day low", false, ""),
    ];
}

/// <summary>Backs the "Lines" dialog: the current list on the left, search and add on the right.</summary>
public sealed partial class LinesViewModel : ObservableObject
{
    private readonly MainViewModel? _main;
    private readonly PriceService? _prices;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? _searchCts;
    private IReadOnlyList<SearchResult> _popular = [];
    private IReadOnlyList<SearchResult> _unfiltered = [];

    public ObservableCollection<LineItemViewModel> Lines { get; } = [];
    public ObservableCollection<SearchResultViewModel> Results { get; } = [];
    public IReadOnlyList<KindFilter> Filters { get; } =
    [
        new("All", null),
        new("Crypto", InstrumentKind.Crypto),
        new("Stocks", InstrumentKind.Stock),
        new("ETFs", InstrumentKind.Etf),
        new("Indices", InstrumentKind.Index),
        new("FX", InstrumentKind.Fx),
        new("Commodities", InstrumentKind.Commodity),
    ];
    public IReadOnlyList<QuoteOption> QuoteOptions => QuoteOption.All;

    [ObservableProperty] private KindFilter _selectedFilter;
    /// <summary>Quote applied to lines added from the results (unless the query named one, "BTC/CAD").</summary>
    [ObservableProperty] private QuoteOption _selectedQuote = QuoteOption.All[0];
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _resultsHeader = "Popular";

    /// <summary>Designer constructor.</summary>
    public LinesViewModel() : this(null, null)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
        var options = new List<ProviderOption>
        {
            new("kraken", "Kraken", "pack://application:,,,/Assets/kraken.png"),
            new("coingecko", "CoinGecko", "pack://application:,,,/Assets/coingecko.png"),
        };
        var defaults = LineConfig.Defaults();
        defaults[0].Alerts.Add(new AlertRule { Type = AlertType.PriceAbove, Threshold = 120_000m });
        defaults[0].Alerts.Add(new AlertRule { Type = AlertType.DownMoreThan, Threshold = 5m });
        defaults[1].Quote = "CAD";
        foreach (var l in defaults)
            Lines.Add(new LineItemViewModel(l, options, (_, _) => { }, (_, _) => { }, () => { }));
        Lines[0].ShowAlerts = true;
        Results.Add(new SearchResultViewModel(new SearchResult(InstrumentKind.Stock, "Shopify Inc.", "SHOP.TO", null, "Toronto", null)));
        Results.Add(new SearchResultViewModel(new SearchResult(InstrumentKind.Crypto, "XRP", "XRP", "ripple", "#5", null) { Quote = "CAD" }));
        ResultsHeader = "Results for \"s\"";
    }

    public LinesViewModel(MainViewModel? main, PriceService? prices)
    {
        _main = main;
        _prices = prices;
        _selectedFilter = Filters[0];
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RunSearchAsync(); };

        if (main is null || prices is null) return;
        foreach (var line in main.Settings.Lines)
            Lines.Add(MakeItem(line));
        _ = RunSearchAsync();
    }

    private LineItemViewModel MakeItem(LineConfig line)
    {
        var options = _prices!.ProvidersFor(line.Kind)
            .Select(p => new ProviderOption(p.Id, p.DisplayName, p.IconUri)).ToList();
        return new LineItemViewModel(line, options,
            (l, providerId) => _ = _main!.SetProviderAsync(l.Id, providerId),
            (l, quote) => _ = _main!.SetQuoteAsync(l.Id, quote),
            () => _main!.AlertsEdited());
    }

    // ---- Left pane ----

    [RelayCommand]
    private void Remove(LineItemViewModel? item)
    {
        if (item is null) return;
        Lines.Remove(item);
        _main?.RemoveLine(item.Line.Id);
    }

    [RelayCommand]
    private void MoveUp(LineItemViewModel? item) => Move(item, -1);

    [RelayCommand]
    private void MoveDown(LineItemViewModel? item) => Move(item, +1);

    private void Move(LineItemViewModel? item, int delta)
    {
        if (item is null) return;
        var i = Lines.IndexOf(item);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Lines.Count) return;
        Lines.Move(i, j);
        _main?.MoveLine(item.Line.Id, delta);
    }

    // ---- Right pane ----

    partial void OnQueryChanged(string value)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    partial void OnSelectedFilterChanged(KindFilter value) => ApplyFilter();

    private async Task RunSearchAsync()
    {
        if (_prices is null) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        var q = Query.Trim();

        IsBusy = true;
        try
        {
            if (q.Length == 0)
            {
                if (_popular.Count == 0) _popular = await _prices.PopularAsync(ct);
                _unfiltered = _popular;
                ResultsHeader = "Popular";
            }
            else
            {
                _unfiltered = await _prices.SearchAsync(q, ct);
                var (b, quote) = PriceService.ParsePair(q);
                ResultsHeader = quote is not null && _unfiltered.Any(r => r.Quote.Length > 0)
                    ? $"{b.ToUpperInvariant()} priced in {quote}"
                    : $"Results for \"{q}\"";
            }
            if (!ct.IsCancellationRequested) ApplyFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ResultsHeader = "Search failed: " + ex.Message;
        }
        finally
        {
            if (!ct.IsCancellationRequested) IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        Results.Clear();
        var kind = SelectedFilter.Kind;
        foreach (var r in _unfiltered.Where(r => kind is null || r.Kind == kind))
            Results.Add(new SearchResultViewModel(r));
        if (Results.Count == 0 && !IsBusy) ResultsHeader = Query.Trim().Length == 0 ? "Nothing to show" : $"No results for \"{Query.Trim()}\"";
    }

    [RelayCommand]
    private async Task AddAsync(SearchResultViewModel? result)
    {
        if (result is null || _prices is null || _main is null || result.IsAdding) return;
        result.IsAdding = true;
        try
        {
            // The query's own quote ("BTC/CAD") wins over the dropdown.
            var r = result.Result.Quote.Length > 0 ? result.Result : result.Result with { Quote = SelectedQuote.Code };
            var providerId = await _prices.PickProviderAsync(r, CancellationToken.None);
            var line = r.ToLine(providerId);
            Lines.Add(MakeItem(line));
            await _main.AddLineAsync(line);
        }
        finally
        {
            result.IsAdding = false;
        }
    }
}

/// <summary>A row in the left pane. Changing the provider or quote re-polls the line; alerts are edited inline.</summary>
public sealed partial class LineItemViewModel : ObservableObject
{
    private readonly Action<LineConfig, string> _onProviderChanged;
    private readonly Action<LineConfig, string> _onQuoteChanged;
    private readonly Action _onAlertsChanged;

    public LineConfig Line { get; }
    public string Name => Line.Name;
    public string Symbol => Line.Symbol;
    public string KindLabel => Kinds.Label(Line.Kind);
    /// <summary>"BTC/CAD · Crypto", "SHOP.TO · Toronto · Stock".</summary>
    public string Detail => string.Join(" · ", new[] { Line.Kind == InstrumentKind.Crypto ? Line.PairLabel : Line.Symbol, Line.Exchange, KindLabel }
        .Where(s => !string.IsNullOrEmpty(s)));
    public string LogoUri => ImageCache.Local(Line.LogoUrl) ?? SelectedProvider?.IconUri ?? "";
    public IReadOnlyList<ProviderOption> ProviderOptions { get; }
    public IReadOnlyList<QuoteOption> QuoteOptions { get; }

    [ObservableProperty] private ProviderOption? _selectedProvider;
    [ObservableProperty] private QuoteOption _selectedQuote;

    // ---- Alerts ----
    public ObservableCollection<AlertRuleViewModel> Alerts { get; } = [];
    public IReadOnlyList<AlertTypeOption> AlertTypes => AlertTypeOption.All;
    public bool HasAlerts => Alerts.Count > 0;
    public int AlertCount => Alerts.Count;

    [ObservableProperty] private bool _showAlerts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddAlertCommand))]
    [NotifyPropertyChangedFor(nameof(NewAlertNeedsValue), nameof(NewAlertUnit))]
    private AlertTypeOption _newAlertType = AlertTypeOption.All[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddAlertCommand))]
    private string _newAlertValue = "";

    public bool NewAlertNeedsValue => NewAlertType.NeedsValue;
    public string NewAlertUnit => NewAlertType.Unit == "$" ? Line.EffectiveQuote : NewAlertType.Unit;

    public LineItemViewModel(LineConfig line, IReadOnlyList<ProviderOption> options,
        Action<LineConfig, string> onProviderChanged, Action<LineConfig, string> onQuoteChanged, Action onAlertsChanged)
    {
        Line = line;
        ProviderOptions = options;
        _onProviderChanged = onProviderChanged;
        _onQuoteChanged = onQuoteChanged;
        _onAlertsChanged = onAlertsChanged;
        _selectedProvider = options.FirstOrDefault(o => o.Id == line.ProviderId) ?? options.FirstOrDefault();
        // The list always contains the line's current quote, even an unusual typed one.
        var current = QuoteOption.For(line.Quote);
        QuoteOptions = QuoteOption.All.Contains(current) ? QuoteOption.All : [.. QuoteOption.All, current];
        _selectedQuote = current;
        foreach (var r in line.Alerts) Alerts.Add(new AlertRuleViewModel(r, line));
        ImageCache.Downloaded += url => { if (url == Line.LogoUrl) OnPropertyChanged(nameof(LogoUri)); };
        Alerts.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAlerts));
            OnPropertyChanged(nameof(AlertCount));
        };
    }

    partial void OnSelectedProviderChanged(ProviderOption? value)
    {
        if (value is null || value.Id == Line.ProviderId) return;
        _onProviderChanged(Line, value.Id);
        OnPropertyChanged(nameof(LogoUri));
    }

    partial void OnSelectedQuoteChanged(QuoteOption value)
    {
        if (value.Code.Equals(Line.Quote, StringComparison.OrdinalIgnoreCase)) return;
        _onQuoteChanged(Line, value.Code);
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(NewAlertUnit));
    }

    [RelayCommand]
    private void ToggleAlerts() => ShowAlerts = !ShowAlerts;

    private bool CanAddAlert() => !NewAlertType.NeedsValue || ParseValue() is > 0;

    [RelayCommand(CanExecute = nameof(CanAddAlert))]
    private void AddAlert()
    {
        var rule = new AlertRule { Type = NewAlertType.Type, Threshold = NewAlertType.NeedsValue ? ParseValue() ?? 0 : 0 };
        Line.Alerts.Add(rule);
        Alerts.Add(new AlertRuleViewModel(rule, Line));
        NewAlertValue = "";
        _onAlertsChanged();
    }

    [RelayCommand]
    private void RemoveAlert(AlertRuleViewModel? item)
    {
        if (item is null) return;
        Alerts.Remove(item);
        Line.Alerts.Remove(item.Rule);
        _onAlertsChanged();
    }

    private decimal? ParseValue() => AlertEngine.ParseThreshold(NewAlertValue);
}

/// <summary>One alert rule in the inline editor.</summary>
public sealed class AlertRuleViewModel(AlertRule rule, LineConfig line)
{
    public AlertRule Rule { get; } = rule;
    public string Text { get; } = AlertEngine.Describe(rule, line);
    public string State => Rule.LastFired is { } t ? $"last fired {t.ToLocalTime():d MMM HH:mm}" : "";
}

/// <summary>A hit in the right pane.</summary>
public sealed partial class SearchResultViewModel : ObservableObject
{
    public SearchResultViewModel(SearchResult result)
    {
        Result = result;
        // First sight of a logo: the cache downloads it once and tells us; until then the initial letter shows.
        ImageCache.Downloaded += url => { if (url == Result.LogoUrl) { OnPropertyChanged(nameof(LogoUri)); OnPropertyChanged(nameof(HasLogo)); } };
    }

    public SearchResult Result { get; }
    public string Name => Result.Name;
    public string Symbol => Result.Symbol;
    /// <summary>"BTC/CAD · #1", "SHOP.TO · Toronto".</summary>
    public string Detail
    {
        get
        {
            var sym = Result.Quote.Length > 0 ? $"{Result.Symbol}/{Result.Quote}" : Result.Symbol;
            return Result.Exchange is null ? sym : $"{sym} · {Result.Exchange}";
        }
    }
    public string KindLabel => Kinds.Label(Result.Kind);
    public string? LogoUri => ImageCache.Local(Result.LogoUrl);
    public bool HasLogo => LogoUri is not null;
    /// <summary>First letter, shown when there is no logo.</summary>
    public string Initial => Result.Symbol.TrimStart('^').Length > 0 ? Result.Symbol.TrimStart('^')[..1] : "?";

    [ObservableProperty] private bool _isAdding;
}

internal static class Kinds
{
    public static string Label(InstrumentKind k) => k switch
    {
        InstrumentKind.Crypto => "Crypto",
        InstrumentKind.Stock => "Stock",
        InstrumentKind.Etf => "ETF",
        InstrumentKind.Index => "Index",
        InstrumentKind.Fx => "FX",
        InstrumentKind.Commodity => "Commodity",
        _ => k.ToString()
    };
}
