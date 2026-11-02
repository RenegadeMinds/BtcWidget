using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

/// <summary>
/// State for the widget window and the settings dialog. The parameterless constructor is for the
/// XAML designer only and fills in sample data; the real one is built in App.OnStartup.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, ITooltipPolicy
{
    private readonly AppSettings _settings;
    private readonly PriceService? _prices;
    private readonly NewsService? _news;

    public ObservableCollection<PriceRowViewModel> Rows { get; } = [];
    public ObservableCollection<LinkViewModel> Links { get; } = [];

    [ObservableProperty] private string _title = "Markets";

    // ---- Appearance / behaviour (mirrors AppSettings, persisted via SettingsChanged) ----
    [ObservableProperty] private double _windowOpacity;
    [ObservableProperty] private double _textOpacity;
    [ObservableProperty] private double _fontSize;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _showInTaskbar;
    [ObservableProperty] private string _toggleHotkey;
    [ObservableProperty] private int _pollIntervalMinutes;
    [ObservableProperty] private int _alertPollMinutes;
    [ObservableProperty] private int _restoreAfterMinutes;
    [ObservableProperty] private bool _restoreOnAlert;
    /// <summary>Mirrors the HKCU Run entry; the registry is the truth.</summary>
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _closeButtonAction;
    [ObservableProperty] private bool _checkForUpdates;
    /// <summary>A newer release exists on GitHub (lights the ? button).</summary>
    [ObservableProperty] private bool _hasUpdate;
    public string? LatestVersion => _settings.LatestVersion;
    public string? LatestUrl => _settings.LatestUrl;
    public string AboutTooltip => HasUpdate ? $"About · version {LatestVersion} is available" : "About";
    public IReadOnlyList<ProviderOption> CloseOptions { get; } =
        [new ProviderOption("exit", "Exits the widget", ""), new ProviderOption("minimize", "Minimizes the widget", "")];
    [ObservableProperty] private double _windowWidth;
    [ObservableProperty] private bool _showDetails;
    [ObservableProperty] private double _listHeight;
    [ObservableProperty] private bool _toastsEnabled;
    [ObservableProperty] private bool _showTooltips;
    [ObservableProperty] private string _rateProvider;
    [ObservableProperty] private string _rateFallback;

    // ---- News ----
    [ObservableProperty] private int _newsCount;
    [ObservableProperty] private int _newsRefreshMinutes;
    [ObservableProperty] private string _newsOpenIn;
    [ObservableProperty] private string _newFeedUrl = "";
    /// <summary>True while a headline newer than the last time the popup was open is available.</summary>
    [ObservableProperty] private bool _hasUnreadNews;
    /// <summary>One row per feed for Settings → News.</summary>
    public ObservableCollection<NewsFeedOption> FeedOptions { get; } = [];
    /// <summary>The same rows grouped by sector, for the collapsible sections in Settings.</summary>
    public ObservableCollection<NewsFeedGroup> FeedGroups { get; } = [];
    /// <summary>Sector for a feed added by URL.</summary>
    [ObservableProperty] private string _newFeedSector = "stocks";
    public IReadOnlyList<ProviderOption> SectorOptions =>
        NewsFeed.KnownSectors.Concat(Feeds.Select(f => f.Sector)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(s => new ProviderOption(s, NewsFeed.SectorLabel(s), "")).ToList();
    public IReadOnlyList<ProviderOption> NewsOpenOptions { get; } =
        [new ProviderOption("browser", "Default browser", ""), new ProviderOption("reader", "Built-in reader window", "")];

    [ObservableProperty] private string _status = "Waiting for first update…";
    [ObservableProperty] private bool _isRefreshing;
    /// <summary>True while any row has an alert that has fired and not been clicked away.</summary>
    [ObservableProperty] private bool _hasPendingAlerts;

    /// <summary>Providers offered as the source of conversion rates (crypto-capable ones).</summary>
    public IReadOnlyList<ProviderOption> RateProviderOptions { get; }

    /// <summary>Raised after any setting or line changes; App persists.</summary>
    public event Action? SettingsChanged;
    public event Action? MinimizeRequested;
    public event Action? CloseRequested;
    public event Action? SettingsRequested;
    public event Action? ManageLinesRequested;
    /// <summary>Open the Alerts dialog, optionally with a line preselected.</summary>
    public event Action<Guid?>? AlertsRequested;
    /// <summary>Toggle the news popup.</summary>
    public event Action? NewsRequested;
    public event Action? AboutRequested;
    /// <summary>Bring the widget back from minimized without taking focus (an alert fired, or the restore timer).</summary>
    public event Action? RestoreRequested;
    /// <summary>Show an article (url, title) in the built-in reader window.</summary>
    public event Action<Uri, string>? ArticleRequested;
    /// <summary>Shows a system notification (title, body) and reports whether it went out. Set by App.</summary>
    public Func<string, string, bool>? Notifier { get; set; }

    public AppSettings Settings => _settings;
    public PriceService? Prices => _prices;
    public NewsService? News => _news;

    /// <summary>Designer constructor: sample data, no services.</summary>
    public MainViewModel() : this(new AppSettings { Lines = LineConfig.Defaults() }, null, null, FeedStore.Defaults())
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;

        Rows.Clear();
        var lines = LineConfig.Defaults();
        Rows.Add(new PriceRowViewModel(lines[0], "Kraken", "pack://application:,,,/Assets/kraken.png", null)
        {
            Price = 109_842.5m, Change = 2.14, Change7d = 5.02, Change30d = -9.8, UpdatedAt = DateTimeOffset.Now,
            High = 121_000m, Low = 104_200m, FromHigh = -9.2, FromLow = 5.4, RangeDays = 30, Series = SampleSeries(1, -0.1),
            IsAlerting = true, AlertText = "Bitcoin crossed above $109,000"
        });
        Rows.Add(new PriceRowViewModel(lines[1], "CoinGecko", "pack://application:,,,/Assets/coingecko.png", null)
        {
            Price = 109_801.0m, Change = 2.09, Change7d = 4.9, Change30d = -10.1, UpdatedAt = DateTimeOffset.Now,
            High = 121_000m, Low = 104_200m, FromHigh = -9.3, FromLow = 5.3, RangeDays = 12, Series = SampleSeries(2, -0.1)
        });
        var tsla = new LineConfig { Kind = InstrumentKind.Stock, Name = "Tesla, Inc.", Symbol = "TSLA", ProviderId = "yahoo" };
        Rows.Add(new PriceRowViewModel(tsla, "Yahoo Finance", "pack://application:,,,/Assets/yahoo.png", null)
        {
            Price = 399.81m, Change = 1.23, Change7d = -2.1, Change30d = 8.4, UpdatedAt = DateTimeOffset.Now,
            High = 401m, Low = 350m, FromHigh = -0.3, FromLow = 14.2, RangeDays = 30, Series = SampleSeries(3, 0.08)
        });
        Status = "Updated 14:05 · next 15:05";
    }

    private static double[] SampleSeries(int seed, double drift)
    {
        var rnd = new Random(seed);
        var s = new double[PriceAnalysis.SeriesLength + 1];
        var v = 100.0;
        for (var i = 0; i < s.Length; i++)
        {
            v += drift + (rnd.NextDouble() - 0.5) * 3;
            s[i] = v;
        }
        return s;
    }

    public MainViewModel(AppSettings settings, PriceService? prices, NewsService? news = null, List<NewsFeed>? feeds = null)
    {
        _settings = settings;
        _prices = prices;
        _news = news;
        Feeds = feeds ?? [];

        _windowOpacity = settings.WindowOpacity;
        _textOpacity = settings.TextOpacity;
        _fontSize = settings.FontSize;
        _alwaysOnTop = settings.AlwaysOnTop;
        _showInTaskbar = settings.ShowInTaskbar;
        _toggleHotkey = settings.ToggleHotkey;
        _pollIntervalMinutes = settings.PollIntervalMinutes;
        _alertPollMinutes = settings.AlertPollMinutes;
        _restoreAfterMinutes = settings.RestoreAfterMinutes;
        _restoreOnAlert = settings.RestoreOnAlert;
        _closeButtonAction = settings.CloseButtonAction;
        _checkForUpdates = settings.CheckForUpdates;
        _startWithWindows = prices is not null && StartupManager.IsEnabled();
        if (_startWithWindows) StartupManager.RepairPath();
        if (settings.LatestVersion is { } lv && UpdateChecker.TryParse(lv, out var latest)) _hasUpdate = latest > UpdateChecker.Current;
        _windowWidth = settings.Width;
        _showDetails = settings.ShowDetails;
        _listHeight = settings.ListHeight;
        _toastsEnabled = settings.ToastsEnabled;
        _showTooltips = settings.ShowTooltips;
        _rateProvider = settings.RateProvider;
        _rateFallback = settings.RateFallback;
        _newsCount = settings.NewsCount;
        _newsRefreshMinutes = settings.NewsRefreshMinutes;
        _newsOpenIn = settings.NewsOpenIn;
        BuildFeedOptions();
        if (news is not null)
        {
            news.MaxAge = TimeSpan.FromHours(Math.Max(1, settings.NewsMaxAgeHours));
            news.SetFeeds(EnabledFeeds());
            news.Updated += OnNewsUpdated;
        }
        RateProviderOptions = prices is null
            ? [new ProviderOption("kraken", "Kraken", "pack://application:,,,/Assets/kraken.png"), new ProviderOption("coingecko", "CoinGecko", "pack://application:,,,/Assets/coingecko.png")]
            : prices.RateProviders.Select(p => new ProviderOption(p.Id, p.DisplayName, p.IconUri)).ToList();

        BuildLinks();
        if (prices is null) return;

        prices.RateProvider = settings.RateProvider;
        prices.RateFallback = settings.RateFallback;
        prices.SetAlertInterval(TimeSpan.FromMinutes(Math.Max(1, settings.AlertPollMinutes)));
        RebuildRows();
        prices.SetLines(settings.Lines);
        prices.Updated += OnUpdated;
        prices.Failed += OnFailed;
    }

    // ---- Lines ----

    private void RebuildRows()
    {
        if (_prices is null) return;
        var existing = Rows.ToDictionary(r => r.Id);
        Rows.Clear();
        foreach (var line in _settings.Lines.Where(l => l.Enabled))
        {
            if (existing.TryGetValue(line.Id, out var row) && row.Line.ProviderId == line.ProviderId && row.Line.Quote == line.Quote)
            {
                Rows.Add(row);
            }
            else if (_prices.Sources.TryGetValue(line.ProviderId, out var src))
            {
                var fresh = new PriceRowViewModel(line, src) { AlertsRequested = id => AlertsRequested?.Invoke(id) };
                fresh.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PriceRowViewModel.IsAlerting)) RefreshStatus(); };
                Rows.Add(fresh);
            }
        }
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var pairs = _settings.Lines.Where(l => l.Enabled).Select(l => l.PairLabel).Distinct().ToList();
        Title = pairs.Count == 1 ? pairs[0].Replace("/", " / ") : "Markets";
    }

    public async Task AddLineAsync(LineConfig line)
    {
        _settings.Lines.Add(line);
        LinesChanged();
        if (_prices is not null) await _prices.PollLineAsync(line);
    }

    public void RemoveLine(Guid id)
    {
        _settings.Lines.RemoveAll(l => l.Id == id);
        LinesChanged();
    }

    public void MoveLine(Guid id, int delta)
    {
        var i = _settings.Lines.FindIndex(l => l.Id == id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _settings.Lines.Count) return;
        (_settings.Lines[i], _settings.Lines[j]) = (_settings.Lines[j], _settings.Lines[i]);
        LinesChanged();
    }

    public async Task SetProviderAsync(Guid id, string providerId)
    {
        var line = _settings.Lines.FirstOrDefault(l => l.Id == id);
        if (line is null || line.ProviderId == providerId) return;
        line.ProviderId = providerId;
        LinesChanged();
        if (_prices is not null) await _prices.PollLineAsync(line);
    }

    public async Task SetQuoteAsync(Guid id, string quote)
    {
        var line = _settings.Lines.FirstOrDefault(l => l.Id == id);
        if (line is null || line.Quote.Equals(quote, StringComparison.OrdinalIgnoreCase)) return;
        line.Quote = quote;
        LinesChanged();
        if (_prices is not null) await _prices.PollLineAsync(line);
    }

    /// <summary>The Lines and Alerts dialogs edit <see cref="LineConfig.Alerts"/> in place; persist and refresh the rows' bells.</summary>
    public void AlertsEdited()
    {
        foreach (var r in Rows) r.AlertsChanged();
        Changed();
        // A line that just got its first alert should start its faster cadence now, not after the current hour.
        _prices?.Nudge();
        RefreshStatus();
    }

    private void LinesChanged()
    {
        _prices?.SetLines(_settings.Lines);
        RebuildRows();
        BuildLinks();
        Changed();
    }

    /// <summary>Footer links follow the first line; which links and their URL templates come from providers.json.</summary>
    private void BuildLinks()
    {
        Links.Clear();
        var first = _settings.Lines.FirstOrDefault(l => l.Enabled)
                    ?? new LineConfig { Kind = InstrumentKind.Crypto, Name = "Bitcoin", Symbol = "BTC", CoinId = "bitcoin" };
        foreach (var link in Providers.Links.Where(l => l.Kinds.Contains(first.Kind)))
        {
            try { Links.Add(new LinkViewModel(link.Name, link.IconUri, new Uri(UrlTemplate.Fill(link.Url, first)))); }
            catch (UriFormatException) { /* a bad template in the user's providers.json: skip that link */ }
        }
    }

    // ---- News ----

    /// <summary>The user's feed list (feeds.json), in file order.</summary>
    public List<NewsFeed> Feeds { get; }

    public List<NewsFeed> EnabledFeeds() => Feeds.Where(f => f.Enabled).ToList();

    public NewsFeed? Feed(string id) => Feeds.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    public string FeedName(string id) => Feed(id)?.Name ?? id;
    public Uri? FeedSite(string id) => Feed(id)?.SiteUri();

    private void BuildFeedOptions()
    {
        FeedOptions.Clear();
        FeedGroups.Clear();
        foreach (var f in Feeds) FeedOptions.Add(new NewsFeedOption(f, this));
        // Known sectors first, in their order; anything else after, alphabetically.
        var sectors = Feeds.Select(f => f.Sector).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => { var i = Array.IndexOf(NewsFeed.KnownSectors, s.ToLowerInvariant()); return i < 0 ? 99 : i; })
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase);
        foreach (var sector in sectors)
        {
            var g = new NewsFeedGroup(sector);
            foreach (var o in FeedOptions.Where(o => o.Feed.Sector.Equals(sector, StringComparison.OrdinalIgnoreCase))) g.Options.Add(o);
            g.IsExpanded = g.Options.Any(o => o.IsEnabled);
            FeedGroups.Add(g);
        }
        OnPropertyChanged(nameof(SectorOptions));
    }

    private NewsFeedGroup GroupFor(string sector)
    {
        var g = FeedGroups.FirstOrDefault(g => g.Sector.Equals(sector, StringComparison.OrdinalIgnoreCase));
        if (g is null)
        {
            g = new NewsFeedGroup(sector) { IsExpanded = true };
            FeedGroups.Add(g);
            OnPropertyChanged(nameof(SectorOptions));
        }
        return g;
    }

    public void SetFeedEnabled(string id, bool enabled)
    {
        var f = Feed(id);
        if (f is null || f.Enabled == enabled) return;
        f.Enabled = enabled;
        var opt = FeedOptions.FirstOrDefault(o => o.Id == id);
        if (opt is not null && opt.IsEnabled != enabled) opt.IsEnabled = enabled;
        FeedGroups.FirstOrDefault(g => g.Sector.Equals(f.Sector, StringComparison.OrdinalIgnoreCase))?.Refresh();
        FeedsChanged(refresh: enabled);
    }

    public void RenameFeed(string id, string name)
    {
        var f = Feed(id);
        name = name.Trim();
        if (f is null || name.Length == 0 || f.Name == name) return;
        f.Name = name;
        FeedsChanged(refresh: false);
    }

    /// <summary>Add a feed by URL (Settings → News). Named after its host until the feed reports its title.</summary>
    [RelayCommand]
    private void AddFeed()
    {
        var url = NewFeedUrl.Trim();
        if (url.Length == 0) return;
        if (!url.Contains("://")) url = "https://" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        if (Feeds.Any(f => string.Equals(f.Url.TrimEnd('/'), uri.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))) { NewFeedUrl = ""; return; }
        var id = FeedStore.IdFor(uri.AbsoluteUri);
        while (Feed(id) is not null) id += "-" + Guid.NewGuid().ToString("N")[..4];
        var feed = new NewsFeed { Id = id, Name = uri.Host.Replace("www.", ""), Url = uri.AbsoluteUri, Enabled = true, Sector = NewFeedSector, Country = "INT" };
        Feeds.Add(feed);
        var option = new NewsFeedOption(feed, this);
        FeedOptions.Add(option);
        GroupFor(feed.Sector).Options.Add(option);
        NewFeedUrl = "";
        FeedsChanged(refresh: true);
    }

    public void RemoveFeed(string id)
    {
        if (Feeds.RemoveAll(f => f.Id == id) == 0) return;
        var opt = FeedOptions.FirstOrDefault(o => o.Id == id);
        if (opt is not null)
        {
            FeedOptions.Remove(opt);
            foreach (var g in FeedGroups) g.Options.Remove(opt);
        }
        FeedsChanged(refresh: false);
    }

    /// <summary>Put back any shipped feed that is missing from the list (by id). Never alters existing entries.</summary>
    [RelayCommand]
    private void RestoreDefaultFeeds()
    {
        var added = FeedStore.RestoreDefaults(Feeds);
        if (added == 0) { Status = "All default feeds are already in the list"; return; }
        BuildFeedOptions();
        FeedsChanged(refresh: true);
        Status = $"Added {added} default feed{(added == 1 ? "" : "s")}";
    }

    /// <summary>Feeds changed in memory: save feeds.json, retune the service, refresh if something was switched on.</summary>
    private void FeedsChanged(bool refresh)
    {
        FeedsSaveRequested?.Invoke();
        _news?.SetFeeds(EnabledFeeds());
        if (refresh) _ = _news?.RefreshAsync();
    }

    /// <summary>Raised when feeds.json should be written; App owns the store.</summary>
    public event Action? FeedsSaveRequested;

    private void OnNewsUpdated() => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        if (_news is null) return;
        // A feed added by URL takes the title the feed itself reports, once.
        var renamed = false;
        foreach (var f in Feeds)
        {
            if (_news.Titles.TryGetValue(f.Id, out var title) && f.Name != title && (f.Name.Length == 0 || f.Name == f.Host))
            {
                f.Name = title;
                FeedOptions.FirstOrDefault(o => o.Id == f.Id)?.NameChanged();
                renamed = true;
            }
        }
        if (renamed) FeedsSaveRequested?.Invoke();
        RefreshUnreadNews();
    });

    // ---- Badge colours ----

    private readonly Dictionary<string, Brush> _badgeBrushes = new();

    /// <summary>
    /// The feed's badge colour: its "color" from feeds.json when set, otherwise a muted hue stepped around the
    /// circle by the golden angle in list order, so neighbours never look alike.
    /// </summary>
    public Brush FeedBrush(string feedId)
    {
        var index = Feeds.FindIndex(f => f.Id.Equals(feedId, StringComparison.OrdinalIgnoreCase));
        var explicitColor = index >= 0 ? Feeds[index].Color : null;
        var key = explicitColor ?? $"#{(index < 0 ? HashHue(feedId) : index * 137.508 % 360):0}";
        lock (_badgeBrushes)
        {
            if (_badgeBrushes.TryGetValue(key, out var b)) return b;
            Brush brush;
            if (explicitColor is not null && TryParseColor(explicitColor, out var c)) brush = new SolidColorBrush(c);
            else brush = new SolidColorBrush(FromHsl(double.Parse(key[1..], System.Globalization.CultureInfo.InvariantCulture), 0.45, 0.42));
            brush.Freeze();
            _badgeBrushes[key] = brush;
            return brush;
        }
    }

    private static double HashHue(string s)
    {
        var hash = 0;
        foreach (var c in s) hash = unchecked(hash * 31 + c);
        return Math.Abs(hash) % 360;
    }

    private static bool TryParseColor(string s, out Color color)
    {
        try { color = (Color)ColorConverter.ConvertFromString(s); return true; }
        catch { color = default; return false; }
    }

    private static Color FromHsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x), 3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x)
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private void RefreshUnreadNews()
    {
        if (_news is null) return;
        var seen = _settings.NewsSeenAt ?? DateTimeOffset.MinValue;
        HasUnreadNews = _news.Items.Take(Math.Max(1, NewsCount)).Any(i => i.Published > seen);
    }

    /// <summary>The popup closed: everything shown so far counts as seen.</summary>
    public void MarkNewsSeen()
    {
        _settings.NewsSeenAt = DateTimeOffset.UtcNow;
        RefreshUnreadNews();
        Changed();
    }

    /// <summary>The popup's pin or filter changed; persist.</summary>
    public void NewsStateChanged() => Changed();

    public void SaveNewsSize(double width, double height)
    {
        if (width < 100 || height < 100) return;
        _settings.NewsWidth = width;
        _settings.NewsHeight = height;
        Changed();
    }

    public void OpenArticle(Uri url, string title)
    {
        if (NewsOpenIn == "reader" && ArticleRequested is not null) ArticleRequested.Invoke(url, title);
        else Shell.OpenUrl(url);
    }

    // ---- Price updates (arrive on a worker thread) ----

    private DateTimeOffset? _lastUpdate;

    private void OnUpdated(PriceUpdate u) => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        var row = Rows.FirstOrDefault(r => r.Id == u.LineId);
        row?.Apply(u);
        _lastUpdate = DateTimeOffset.Now;

        var line = _settings.Lines.FirstOrDefault(l => l.Id == u.LineId);
        if (line is not null && line.Alerts.Count > 0)
        {
            var fired = AlertEngine.Evaluate(line, u, out var stateChanged);
            foreach (var f in fired)
            {
                row?.Alert(f.Message);
                var notified = ToastsEnabled && (Notifier?.Invoke($"{line.Name} ({line.Symbol})", f.Message) ?? false);
                AlertLog.Append(f.Message, notified);
                if (f.Rule.Restore ?? RestoreOnAlert) RestoreRequested?.Invoke();
            }
            if (stateChanged) Changed();
        }
        RefreshStatus();
    });

    /// <summary>Footer text: the newest pending alert while any row is alerting, otherwise the poll times.</summary>
    private void RefreshStatus()
    {
        var alert = Rows.Where(r => r.IsAlerting).Select(r => r.AlertText).LastOrDefault(t => t is not null);
        HasPendingAlerts = alert is not null;
        if (alert is not null) { Status = alert; return; }
        if (_lastUpdate is not { } t) return;
        var next = _prices?.NextDue?.ToLocalTime() ?? t.AddMinutes(PollIntervalMinutes);
        var watched = _settings.Lines.Count(l => l.Enabled && PriceService.IsWatched(l));
        var s = $"Updated {t:HH:mm} · next {next:HH:mm}";
        if (watched > 0 && AlertPollMinutes < PollIntervalMinutes)
            s += $" · {watched} alert line{(watched == 1 ? "" : "s")} every {AlertPollMinutes} min";
        Status = s;
    }

    private void OnFailed(Guid lineId, IPriceSource? src, Exception ex) => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        Rows.FirstOrDefault(r => r.Id == lineId)?.Fail(ex);
        Status = $"{src?.DisplayName ?? "Provider"} failed {DateTimeOffset.Now:HH:mm}";
    });

    // ---- Commands ----

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_prices is null || IsRefreshing) return;
        IsRefreshing = true;
        try { await _prices.RefreshNowAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand] private void Minimize() => MinimizeRequested?.Invoke();
    /// <summary>The title-bar X: exit, or minimize when Settings says so (the tray menu always has Exit).</summary>
    [RelayCommand] private void Close()
    {
        if (CloseButtonAction == "minimize") MinimizeRequested?.Invoke(); else CloseRequested?.Invoke();
    }
    [RelayCommand] private void OpenAbout() => AboutRequested?.Invoke();

    /// <summary>Store the result of an update check (manual or daily) and light the ? button if it is newer.</summary>
    public void RecordUpdateCheck(UpdateInfo? info)
    {
        _settings.LastUpdateCheck = DateTimeOffset.UtcNow;
        if (info is not null)
        {
            _settings.LatestVersion = info.Latest.ToString(3);
            _settings.LatestUrl = info.Url;
            HasUpdate = info.IsNewer;
            OnPropertyChanged(nameof(LatestVersion));
            OnPropertyChanged(nameof(LatestUrl));
        }
        Changed();
    }

    partial void OnHasUpdateChanged(bool value) => OnPropertyChanged(nameof(AboutTooltip));
    [RelayCommand] private void OpenSettings() => SettingsRequested?.Invoke();
    [RelayCommand] private void ManageLines() => ManageLinesRequested?.Invoke();
    [RelayCommand] private void OpenAlerts() => AlertsRequested?.Invoke(null);
    [RelayCommand] private void OpenNews() => NewsRequested?.Invoke();

    // ---- Settings persistence ----

    partial void OnWindowOpacityChanged(double value) { _settings.WindowOpacity = value; Changed(); }
    partial void OnTextOpacityChanged(double value) { _settings.TextOpacity = value; Changed(); }
    partial void OnFontSizeChanged(double value) { _settings.FontSize = value; Changed(); }
    partial void OnAlwaysOnTopChanged(bool value) { _settings.AlwaysOnTop = value; Changed(); }
    partial void OnShowInTaskbarChanged(bool value) { _settings.ShowInTaskbar = value; Changed(); }
    partial void OnToggleHotkeyChanged(string value) { _settings.ToggleHotkey = value; Changed(); }
    partial void OnWindowWidthChanged(double value) { _settings.Width = value; Changed(); }
    partial void OnShowDetailsChanged(bool value) { _settings.ShowDetails = value; Changed(); }
    partial void OnListHeightChanged(double value) { _settings.ListHeight = Math.Max(0, value); Changed(); }
    partial void OnToastsEnabledChanged(bool value) { _settings.ToastsEnabled = value; Changed(); }
    partial void OnShowTooltipsChanged(bool value) { _settings.ShowTooltips = value; Changed(); }
    partial void OnRateProviderChanged(string value)
    {
        _settings.RateProvider = value;
        if (_prices is not null) _prices.RateProvider = value;
        Changed();
    }
    partial void OnRateFallbackChanged(string value)
    {
        _settings.RateFallback = value;
        if (_prices is not null) _prices.RateFallback = value;
        Changed();
    }
    partial void OnNewsCountChanged(int value) { _settings.NewsCount = Math.Clamp(value, 1, 200); RefreshUnreadNews(); Changed(); }
    partial void OnNewsOpenInChanged(string value) { _settings.NewsOpenIn = value; Changed(); }
    partial void OnNewsRefreshMinutesChanged(int value)
    {
        _settings.NewsRefreshMinutes = Math.Max(1, value);
        _news?.SetInterval(TimeSpan.FromMinutes(_settings.NewsRefreshMinutes));
        Changed();
    }
    partial void OnPollIntervalMinutesChanged(int value)
    {
        _settings.PollIntervalMinutes = Math.Max(1, value);
        _prices?.SetInterval(TimeSpan.FromMinutes(_settings.PollIntervalMinutes));
        RefreshStatus();
        Changed();
    }
    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_prices is null) return;
        if (!StartupManager.Set(value)) Status = "Could not change the Windows startup entry";
    }
    partial void OnCloseButtonActionChanged(string value) { _settings.CloseButtonAction = value; Changed(); }
    partial void OnCheckForUpdatesChanged(bool value) { _settings.CheckForUpdates = value; Changed(); }
    partial void OnRestoreAfterMinutesChanged(int value) { _settings.RestoreAfterMinutes = Math.Max(0, value); Changed(); }
    partial void OnRestoreOnAlertChanged(bool value) { _settings.RestoreOnAlert = value; Changed(); }
    partial void OnAlertPollMinutesChanged(int value)
    {
        _settings.AlertPollMinutes = Math.Max(1, value);
        _prices?.SetAlertInterval(TimeSpan.FromMinutes(_settings.AlertPollMinutes));
        RefreshStatus();
        Changed();
    }

    private void Changed() => SettingsChanged?.Invoke();
}
