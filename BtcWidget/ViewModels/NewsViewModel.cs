using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

/// <summary>Windows whose tooltips follow the "Tooltips when hovering" setting implement this on their DataContext.</summary>
public interface ITooltipPolicy
{
    bool ShowTooltips { get; }
}

/// <summary>"All sources" or one feed, for the filter box in the popup header.</summary>
public sealed record SourceChoice(string? FeedId, string? Sector, string Name)
{
    public override string ToString() => Name;
    public bool Matches(NewsItem item, Func<string, NewsFeed?> feed) =>
        FeedId is null && Sector is null
        || FeedId is not null && item.FeedId.Equals(FeedId, StringComparison.OrdinalIgnoreCase)
        || Sector is not null && (feed(item.FeedId)?.Sector.Equals(Sector, StringComparison.OrdinalIgnoreCase) ?? false);
}

/// <summary>A date separator row in the headline list: "Today", "Yesterday", "Mon 27 Oct".</summary>
public sealed class NewsDateHeader(string text)
{
    public string Text { get; } = text;
}

/// <summary>
/// The news popup: a merged, newest-first list of headlines from the enabled feeds, with date separators.
/// Headlines newer than the last time the popup was open are shown bright, the rest muted. Clicking one
/// opens it in the default browser or the built-in reader (Settings → News).
/// </summary>
public sealed partial class NewsViewModel : ObservableObject, ITooltipPolicy
{
    private readonly MainViewModel? _main;
    private readonly NewsService? _news;
    private readonly DateTimeOffset _seenBefore;

    public ObservableCollection<object> Items { get; } = [];
    public ObservableCollection<SourceChoice> Sources { get; } = [];

    [ObservableProperty] private SourceChoice? _selectedSource;
    [ObservableProperty] private string _status = "Loading…";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private double _fontSize = 13;
    /// <summary>Pinned: the popup stays open when it loses focus.</summary>
    [ObservableProperty] private bool _isPinned;
    /// <summary>"bitcoin, gold, bank": a headline is shown when its title or source contains any of the terms.</summary>
    [ObservableProperty] private string _filter = "";
    public bool HasFilter => FilterTerms(Filter).Length > 0;

    public bool ShowTooltips => _main?.ShowTooltips ?? true;
    public event Action? CloseRequested;

    /// <summary>Designer constructor: sample headlines.</summary>
    public NewsViewModel() : this(null, null)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
        var now = DateTimeOffset.Now;
        var cd = new NewsFeed { Id = "coindesk", Name = "CoinDesk" };
        var zh = new NewsFeed { Id = "zerohedge", Name = "ZeroHedge" };
        Items.Add(new NewsDateHeader("Today"));
        Items.Add(new NewsItemViewModel(new NewsItem(cd.Id, cd.Name, "Bitcoin holds $110K as ETF inflows resume after three-week pause", new Uri("https://example.com/1"), now.AddMinutes(-12)), true, this));
        Items.Add(new NewsItemViewModel(new NewsItem(zh.Id, zh.Name, "Futures Slide As Yields Jump Ahead Of Fed Decision", new Uri("https://example.com/2"), now.AddHours(-3)), true, this));
        Items.Add(new NewsDateHeader("Yesterday"));
        Items.Add(new NewsItemViewModel(new NewsItem(cd.Id, cd.Name, "Kraken expands derivatives to the EU", new Uri("https://example.com/3"), now.AddDays(-1)), false, this));
        HasItems = true;
        Status = "Updated 14:05 · 6 feeds";
    }

    public NewsViewModel(MainViewModel? main, NewsService? news)
    {
        _main = main;
        _news = news;
        _seenBefore = main?.Settings.NewsSeenAt ?? DateTimeOffset.MinValue;
        _fontSize = main?.FontSize ?? 13;
        _isPinned = main?.Settings.NewsPinned ?? false;
        _filter = main?.Settings.NewsFilter ?? "";

        Sources.Add(new SourceChoice(null, null, "All sources"));
        if (main is not null)
        {
            var enabled = main.EnabledFeeds();
            // Sector entries (only sectors with something on), then the feeds themselves.
            var sectors = enabled.Select(f => f.Sector).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => { var i = Array.IndexOf(NewsFeed.KnownSectors, s.ToLowerInvariant()); return i < 0 ? 99 : i; }).ToList();
            if (sectors.Count > 1)
                foreach (var s in sectors) Sources.Add(new SourceChoice(null, s, NewsFeed.SectorLabel(s)));
            foreach (var f in enabled.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
                Sources.Add(new SourceChoice(f.Id, null, sectors.Count > 1 ? $"   {f.Name}" : f.Name));
        }
        _selectedSource = Sources[0];

        if (news is null) return;
        news.Updated += OnUpdated;
        Rebuild();
    }

    /// <summary>Stop listening to the service; the window calls this when it closes.</summary>
    public void Detach()
    {
        if (_news is not null) _news.Updated -= OnUpdated;
    }

    private void OnUpdated() => Application.Current?.Dispatcher.BeginInvoke(Rebuild);

    partial void OnSelectedSourceChanged(SourceChoice? value) => Rebuild();

    partial void OnIsPinnedChanged(bool value)
    {
        if (_main is null) return;
        _main.Settings.NewsPinned = value;
        _main.NewsStateChanged();
    }

    partial void OnFilterChanged(string value)
    {
        OnPropertyChanged(nameof(HasFilter));
        if (_main is not null) { _main.Settings.NewsFilter = value; _main.NewsStateChanged(); }
        Rebuild();
    }

    [RelayCommand] private void ClearFilter() => Filter = "";

    /// <summary>Split on commas (and semicolons); a term may contain spaces, so "interest rate" works.</summary>
    public static string[] FilterTerms(string? filter) =>
        (filter ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0).ToArray();

    public static bool MatchesFilter(NewsItem item, string[] terms) =>
        terms.Length == 0 || terms.Any(t => item.Title.Contains(t, StringComparison.CurrentCultureIgnoreCase)
                                          || item.Source.Contains(t, StringComparison.CurrentCultureIgnoreCase));

    private void Rebuild()
    {
        if (_news is null || _main is null) return;
        var count = Math.Max(1, _main.NewsCount);
        var choice = SelectedSource ?? Sources[0];
        var terms = FilterTerms(Filter);
        // Filter first, then take: the list fills up to the headline count with matches only.
        var items = _news.Items.Where(i => choice.Matches(i, _main.Feed) && MatchesFilter(i, terms)).Take(count).ToList();

        Items.Clear();
        string? lastHeader = null;
        foreach (var i in items)
        {
            var header = DateHeader(i.Published.ToLocalTime());
            if (header != lastHeader)
            {
                Items.Add(new NewsDateHeader(header));
                lastHeader = header;
            }
            Items.Add(new NewsItemViewModel(i, i.Published > _seenBefore, this));
        }
        HasItems = Items.Count > 0;

        var feeds = _main.EnabledFeeds().Count;
        if (_news.LastRefresh is not { } t)
        {
            Status = feeds == 0 ? "No feeds enabled. Pick some in Settings → News." : "Loading…";
            return;
        }
        if (items.Count == 0 && terms.Length > 0)
        {
            Status = $"Nothing matches \"{string.Join(", ", terms)}\" in the last {_news.Items.Count} headlines";
            return;
        }
        var failed = _news.Failures.Count;
        var s = $"Updated {t:HH:mm} · {feeds} feed{(feeds == 1 ? "" : "s")}";
        if (terms.Length > 0) s += $" · {items.Count} match{(items.Count == 1 ? "" : "es")}";
        if (failed > 0)
        {
            var names = _news.Failures.Keys.Select(id => _main.FeedName(id)).ToList();
            s += $" · {string.Join(", ", names)} failed";
        }
        Status = s;
    }

    public static string DateHeader(DateTimeOffset local)
    {
        var today = DateTime.Today;
        var d = local.Date;
        if (d == today) return "Today";
        if (d == today.AddDays(-1)) return "Yesterday";
        return d.Year == today.Year ? local.ToString("ddd d MMM") : local.ToString("d MMM yyyy");
    }

    // ---- Actions (called by the item view models) ----

    public void Open(NewsItemViewModel item) => _main?.OpenArticle(item.Item.Link, item.Item.Title);

    public void OpenInBrowser(NewsItemViewModel item) => Shell.OpenUrl(item.Item.Link);

    public static void CopyLink(NewsItemViewModel item)
    {
        try { Clipboard.SetText(item.Item.Link.AbsoluteUri); } catch { /* clipboard busy */ }
    }

    public void VisitSource(NewsItemViewModel item)
    {
        var site = _main?.FeedSite(item.Item.FeedId);
        if (site is not null) Shell.OpenUrl(site);
    }

    /// <summary>Switch the feed off (same as unticking it in Settings) and drop its headlines.</summary>
    public void HideSource(NewsItemViewModel item)
    {
        if (_main is null) return;
        _main.SetFeedEnabled(item.Item.FeedId, false);
        var choice = Sources.FirstOrDefault(s => s.FeedId == item.Item.FeedId);
        if (choice is not null)
        {
            if (SelectedSource == choice) SelectedSource = Sources[0];
            Sources.Remove(choice);
        }
        Rebuild();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_news is null || IsRefreshing) return;
        IsRefreshing = true;
        try { await _news.RefreshAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand] private void OpenSettings() => _main?.OpenSettingsCommand.Execute(null);
    [RelayCommand] private void Close() => CloseRequested?.Invoke();

    /// <summary>"reader" / "browser": used by the row menu to offer the other one.</summary>
    public bool OpensInReader => _main?.NewsOpenIn == "reader";

    public Brush BadgeBrush(string feedId) => _main?.FeedBrush(feedId) ?? Brushes.Gray;
    public string? FeedTag(string feedId) => _main?.Feed(feedId)?.Tag;
}

/// <summary>One headline row.</summary>
public sealed partial class NewsItemViewModel : ObservableObject
{
    private readonly NewsViewModel _parent;

    public NewsItemViewModel(NewsItem item, bool isNew, NewsViewModel parent)
    {
        Item = item;
        IsNew = isNew;
        _parent = parent;
        Badge = Initials(item.Source);
        BadgeBrush = parent.BadgeBrush(item.FeedId);
        AltOpenHeader = parent.OpensInReader ? "Open in browser" : "Open in reader";
    }

    public NewsItem Item { get; }
    public string Title => Item.Title;
    public string Source => Item.Source;
    public string TimeText => Item.Published.ToLocalTime().ToString("HH:mm");
    public bool IsNew { get; }
    public string Badge { get; }
    public Brush BadgeBrush { get; }
    public string AltOpenHeader { get; }
    public string VisitHeader => $"Visit {Source}";
    public string HideHeader => $"Hide {Source}";
    public string Tooltip => $"{Title}\n{Source}{(_parent.FeedTag(Item.FeedId) is { } t ? $" ({t})" : "")} · {Item.Published.ToLocalTime():ddd d MMM HH:mm}";

    [RelayCommand] private void Open() => _parent.Open(this);
    [RelayCommand] private void AltOpen()
    {
        if (_parent.OpensInReader) _parent.OpenInBrowser(this); else _parent.Open(this);
    }
    [RelayCommand] private void CopyLink() => NewsViewModel.CopyLink(this);
    [RelayCommand] private void VisitSource() => _parent.VisitSource(this);
    [RelayCommand] private void HideSource() => _parent.HideSource(this);

    /// <summary>"CoinDesk" → CD, "The Block" → TB, "Decrypt" → De, "U.Today" → UT.</summary>
    public static string Initials(string name)
    {
        var words = name.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
        if (words.Length == 0) return "?";
        var w = words[0];
        var caps = w.Where(char.IsUpper).Take(2).ToArray();
        if (caps.Length == 2) return new string(caps);
        return w.Length >= 2 ? $"{char.ToUpperInvariant(w[0])}{char.ToLowerInvariant(w[1])}" : w.ToUpperInvariant();
    }
}
