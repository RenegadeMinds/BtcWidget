using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

/// <summary>A line to pick in the "new alert" composer: "Tesla (TSLA)", "Bitcoin (BTC/CAD · Kraken)".</summary>
public sealed record LineChoice(LineConfig Line, string Label);

/// <summary>
/// Backs the Alerts dialog: one composer at the top (line, condition, value) and every rule across all lines
/// below it, with an enable switch and a remove button each. Rules live in <see cref="LineConfig.Alerts"/>;
/// this edits them in place and asks <see cref="MainViewModel"/> to persist.
/// </summary>
public sealed partial class AlertsViewModel : ObservableObject
{
    private readonly MainViewModel? _main;

    public ObservableCollection<AlertEntryViewModel> Entries { get; } = [];
    public IReadOnlyList<LineChoice> LineChoices { get; }
    public IReadOnlyList<AlertTypeOption> AlertTypes => AlertTypeOption.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewAlertUnit), nameof(Hint))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private LineChoice? _selectedLine;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewAlertNeedsValue), nameof(NewAlertUnit), nameof(Hint))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private AlertTypeOption _newAlertType = AlertTypeOption.All[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newAlertValue = "";

    public bool HasEntries => Entries.Count > 0;
    public bool HasLines => LineChoices.Count > 0;
    public bool NewAlertNeedsValue => NewAlertType.NeedsValue;
    public string NewAlertUnit => NewAlertType.Unit == "$" ? CurrencyOf(SelectedLine?.Line) : NewAlertType.Unit;

    public bool ToastsEnabled
    {
        get => _main?.ToastsEnabled ?? true;
        set { if (_main is not null) _main.ToastsEnabled = value; }
    }

    /// <summary>One line of context under the composer: the current price, the change window, or the history needed.</summary>
    public string Hint
    {
        get
        {
            if (SelectedLine is null) return "Add a line to the widget first.";
            var line = SelectedLine.Line;
            var row = RowOf(line);
            var window = line.IsDaily ? "since the previous close" : "over the last 24 hours";
            return NewAlertType.Type switch
            {
                AlertType.PriceAbove or AlertType.PriceBelow => row?.Price is { } p
                    ? $"{line.Name} is {PriceFormat.Price(p, row.Currency, line.Kind)} now. Fires once when the price crosses the level, then re-arms when it moves back."
                    : "Fires once when the price crosses the level, then re-arms when it moves back.",
                AlertType.UpMoreThan or AlertType.DownMoreThan =>
                    $"Change {window}" + (row?.Change is { } c ? $"; currently {PriceFormat.Percent(c)}." : ".") + " Fires once per move.",
                _ => $"Needs at least {AlertEngine.MinRangeDaysForExtremes} days of readings" +
                     (row is not null ? $"; {line.Name} has {row.RangeDays}." : ".") + " At most one notification per 12 hours.",
            };
        }
    }

    /// <summary>Designer constructor.</summary>
    public AlertsViewModel() : this(null, null)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
        var lines = LineConfig.Defaults();
        var tsla = new LineConfig { Kind = InstrumentKind.Stock, Name = "Tesla, Inc.", Symbol = "TSLA", ProviderId = "yahoo", Exchange = "NASDAQ" };
        lines.Add(tsla);
        lines[0].Alerts.Add(new AlertRule { Type = AlertType.PriceAbove, Threshold = 120_000m });
        lines[0].Alerts.Add(new AlertRule { Type = AlertType.DownMoreThan, Threshold = 5m, LastFired = DateTimeOffset.Now.AddHours(-3) });
        tsla.Alerts.Add(new AlertRule { Type = AlertType.PriceAbove, Threshold = 410m, Enabled = false });
        LineChoices = lines.Select(l => new LineChoice(l, Label(l))).ToList();
        _selectedLine = LineChoices[^1];
        foreach (var l in lines)
            foreach (var r in l.Alerts)
                Entries.Add(new AlertEntryViewModel(l, r, "USD", () => { }, () => true));
    }

    public AlertsViewModel(MainViewModel? main, Guid? preselect)
    {
        _main = main;
        LineChoices = main is null ? [] : main.Settings.Lines.Where(l => l.Enabled).Select(l => new LineChoice(l, Label(l))).ToList();
        _selectedLine = LineChoices.FirstOrDefault(c => c.Line.Id == preselect) ?? LineChoices.FirstOrDefault();
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEntries));
        Rebuild();
    }

    private static string Label(LineConfig l)
    {
        var what = l.Kind == InstrumentKind.Crypto ? l.PairLabel : l.Symbol;
        return $"{l.Name} ({what})";
    }

    private PriceRowViewModel? RowOf(LineConfig line) => _main?.Rows.FirstOrDefault(r => r.Id == line.Id);

    private string CurrencyOf(LineConfig? line)
    {
        if (line is null) return "";
        if (RowOf(line) is { Price: not null } row) return row.Currency;
        return line.EffectiveQuote.Length > 0 ? line.EffectiveQuote : "USD";
    }

    private void Rebuild()
    {
        Entries.Clear();
        if (_main is null) return;
        foreach (var line in _main.Settings.Lines)
            foreach (var rule in line.Alerts)
                Entries.Add(new AlertEntryViewModel(line, rule, CurrencyOf(line), Persist, () => _main?.RestoreOnAlert ?? true));
    }

    private void Persist() => _main?.AlertsEdited();

    private bool CanAdd() => SelectedLine is not null && (!NewAlertType.NeedsValue || AlertEngine.ParseThreshold(NewAlertValue) is > 0);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (SelectedLine is null) return;
        var rule = new AlertRule
        {
            Type = NewAlertType.Type,
            Threshold = NewAlertType.NeedsValue ? AlertEngine.ParseThreshold(NewAlertValue) ?? 0 : 0
        };
        SelectedLine.Line.Alerts.Add(rule);
        NewAlertValue = "";
        Persist();
        Rebuild();
    }

    [RelayCommand]
    private void Remove(AlertEntryViewModel? entry)
    {
        if (entry is null) return;
        entry.Line.Alerts.Remove(entry.Rule);
        Entries.Remove(entry);
        Persist();
    }

    [RelayCommand]
    private void OpenLog()
    {
        if (!System.IO.File.Exists(AlertLog.Path))
            System.IO.File.WriteAllText(AlertLog.Path, "No alerts have fired yet." + Environment.NewLine);
        Shell.OpenUrl(new Uri(AlertLog.Path));
    }
}

/// <summary>One rule in the Alerts dialog's list.</summary>
public sealed partial class AlertEntryViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public LineConfig Line { get; }
    public AlertRule Rule { get; }
    /// <summary>"Tesla (TSLA)", "Bitcoin (BTC/CAD)".</summary>
    public string LineLabel { get; }
    /// <summary>"Price above $410", "Down more than 5% (24h)".</summary>
    public string Text { get; }
    /// <summary>"last fired 1 Nov 14:05", "fired, waiting to re-arm", "watching", "off".</summary>
    public string State => !Enabled ? "off"
        : Rule.LastFired is { } t ? $"last fired {t.ToLocalTime():d MMM HH:mm}" + (Rule.Armed ? "" : " · waiting to re-arm")
        : "watching";

    [ObservableProperty] private bool _enabled;
    /// <summary>Three-state: null follows the Settings default, true / false override it for this rule.</summary>
    [ObservableProperty] private bool? _restore;
    private readonly Func<bool> _defaultRestore;

    /// <summary>"show widget: default (yes)", "show widget: no".</summary>
    public string RestoreText => Restore is { } r ? (r ? "show widget: yes" : "show widget: no")
        : $"show widget: default ({(_defaultRestore() ? "yes" : "no")})";
    public string RestoreTooltip => "Bring the widget back from minimized when this alert fires. Click to cycle: default → yes → no. The default is in Settings → Behaviour.";

    public AlertEntryViewModel(LineConfig line, AlertRule rule, string currency, Action onChanged, Func<bool> defaultRestore)
    {
        Line = line;
        Rule = rule;
        _onChanged = onChanged;
        _defaultRestore = defaultRestore;
        _enabled = rule.Enabled;
        _restore = rule.Restore;
        LineLabel = $"{line.Name} ({(line.Kind == InstrumentKind.Crypto ? line.PairLabel : line.Symbol)})";
        Text = AlertEngine.Describe(rule, line, currency);
    }

    partial void OnEnabledChanged(bool value)
    {
        Rule.Enabled = value;
        Rule.Armed = true;          // switching a rule back on starts it fresh
        OnPropertyChanged(nameof(State));
        _onChanged();
    }

    partial void OnRestoreChanged(bool? value)
    {
        Rule.Restore = value;
        OnPropertyChanged(nameof(RestoreText));
        _onChanged();
    }

    /// <summary>default → yes → no → default.</summary>
    [RelayCommand]
    private void CycleRestore() => Restore = Restore switch { null => true, true => false, false => null };
}
