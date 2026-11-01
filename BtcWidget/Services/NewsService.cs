using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>One headline from one feed.</summary>
public sealed record NewsItem(string FeedId, string Source, string Title, Uri Link, DateTimeOffset Published);

/// <summary>
/// Fetches the enabled RSS / Atom feeds on a timer and keeps one merged list of headlines, newest first.
/// Feeds are fetched in parallel and independently: one dead site never hides the others. Parsing is
/// deliberately loose (RSS 2.0, RSS 1.0 / RDF, Atom, several date formats) because feeds in the wild are.
/// </summary>
public sealed partial class NewsService : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, List<NewsItem>> _byFeed = new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Timer? _timer;
    private TimeSpan _interval;
    private IReadOnlyList<NewsFeed> _feeds = [];

    public NewsService(TimeSpan interval)
    {
        _interval = interval;
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) BtcWidget/1.2 (+rss reader)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.5");
    }

    /// <summary>Headlines older than this are dropped.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(48);

    /// <summary>Merged headlines of the current feeds, newest first.</summary>
    public IReadOnlyList<NewsItem> Items { get; private set; } = [];
    public DateTimeOffset? LastRefresh { get; private set; }
    /// <summary>Feeds that failed on the last refresh, with the reason.</summary>
    public IReadOnlyDictionary<string, string> Failures { get; private set; } = new Dictionary<string, string>();
    /// <summary>Feed titles as the feeds themselves state them (channel/title, feed/title), by feed id.</summary>
    public IReadOnlyDictionary<string, string> Titles { get; private set; } = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _titles = new();

    /// <summary>Raised (on a worker thread) after every refresh, successful or not.</summary>
    public event Action? Updated;

    /// <summary>Replace the set of feeds. Cached items of feeds that stay keep showing while the rest load.</summary>
    public void SetFeeds(IEnumerable<NewsFeed> feeds)
    {
        _feeds = feeds.Where(f => Uri.TryCreate(f.Url, UriKind.Absolute, out _)).ToList();
        var ids = _feeds.Select(f => f.Id).ToHashSet();
        lock (_gate)
        {
            foreach (var k in _byFeed.Keys.Where(k => !ids.Contains(k)).ToList()) _byFeed.Remove(k);
            Merge();
        }
        Updated?.Invoke();
    }

    public void Start()
    {
        _timer ??= new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, _interval);
    }

    public void SetInterval(TimeSpan interval)
    {
        _interval = interval;
        _timer?.Change(interval, interval);
    }

    /// <summary>Fetch every feed now. Safe to call while a refresh is running (it waits for it).</summary>
    public async Task RefreshAsync()
    {
        if (_cts.IsCancellationRequested || _feeds.Count == 0) return;
        await _refreshGate.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            var feeds = _feeds;
            var failures = new Dictionary<string, string>();
            var results = await Task.WhenAll(feeds.Select(f => FetchAsync(f, _cts.Token))).ConfigureAwait(false);
            lock (_gate)
            {
                for (var i = 0; i < feeds.Count; i++)
                {
                    var (items, title, error) = results[i];
                    if (error is not null) { failures[feeds[i].Id] = error; continue; }
                    _byFeed[feeds[i].Id] = items!;
                    if (!string.IsNullOrWhiteSpace(title)) _titles[feeds[i].Id] = title;
                }
                Failures = failures;
                Titles = new Dictionary<string, string>(_titles);
                LastRefresh = DateTimeOffset.Now;
                Merge();
            }
            Updated?.Invoke();
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!_cts.IsCancellationRequested) _refreshGate.Release();
        }
    }

    private void Merge()
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Items = _byFeed.Values.SelectMany(v => v)
            .Where(i => i.Published >= cutoff && seen.Add(i.Link.AbsoluteUri))
            .OrderByDescending(i => i.Published)
            .ToList();
    }

    private async Task<(List<NewsItem>? Items, string? Title, string? Error)> FetchAsync(NewsFeed feed, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return (null, null, $"HTTP {(int)resp.StatusCode}");
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct).ConfigureAwait(false);
            var items = Parse(doc, feed);
            return items.Count == 0 ? (null, null, "no items") : (items, FeedTitle(doc), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return (null, null, ex is TaskCanceledException ? "timed out" : ex.Message);
        }
    }

    /// <summary>The feed's own title: RSS channel/title or Atom feed/title. Suffixes like " RSS Feed" are trimmed.</summary>
    public static string? FeedTitle(XDocument doc)
    {
        var root = doc.Root;
        if (root is null) return null;
        var el = root.Name == Atom + "feed"
            ? root.Element(Atom + "title")
            : root.Descendants().FirstOrDefault(x => x.Name.LocalName == "channel")?.Elements().FirstOrDefault(x => x.Name.LocalName == "title");
        var t = Clean(el?.Value);
        foreach (var suffix in new[] { " RSS Feed", " RSS", " Feed", " News Feed" })
            if (t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && t.Length > suffix.Length + 2) t = t[..^suffix.Length].TrimEnd(' ', '-', '|', ':');
        return t.Length == 0 ? null : t;
    }

    // ---- Parsing ----

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>Items from an RSS 2.0 channel, an RSS 1.0 (RDF) document or an Atom feed.</summary>
    public static List<NewsItem> Parse(XDocument doc, NewsFeed feed)
    {
        var root = doc.Root;
        var list = new List<NewsItem>();
        if (root is null) return list;
        var fetched = DateTimeOffset.UtcNow;

        if (root.Name == Atom + "feed")
        {
            foreach (var e in root.Elements(Atom + "entry"))
            {
                var title = Clean(e.Element(Atom + "title")?.Value);
                var href = e.Elements(Atom + "link")
                    .Where(l => (string?)l.Attribute("rel") is null or "alternate")
                    .Select(l => (string?)l.Attribute("href")).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
                var when = ParseDate(e.Element(Atom + "published")?.Value) ?? ParseDate(e.Element(Atom + "updated")?.Value) ?? fetched;
                Add(list, feed, title, href, when);
            }
            return list;
        }

        // RSS 2.0 (channel/item) and RSS 1.0 (rdf:RDF/item), namespace-agnostic on the element names.
        foreach (var e in root.Descendants().Where(x => x.Name.LocalName == "item"))
        {
            var title = Clean(Child(e, "title"));
            var href = Child(e, "link");
            if (string.IsNullOrWhiteSpace(href))
            {
                // Some feeds put the URL in <guid isPermaLink="true"> or in an Atom link element.
                var guid = e.Elements().FirstOrDefault(x => x.Name.LocalName == "guid");
                if (guid is not null && !string.Equals((string?)guid.Attribute("isPermaLink"), "false", StringComparison.OrdinalIgnoreCase)
                    && Uri.IsWellFormedUriString(guid.Value.Trim(), UriKind.Absolute))
                    href = guid.Value;
                href ??= (string?)e.Element(Atom + "link")?.Attribute("href");
            }
            var when = ParseDate(Child(e, "pubDate")) ?? ParseDate(e.Element(Dc + "date")?.Value) ?? ParseDate(Child(e, "date")) ?? fetched;
            Add(list, feed, title, href, when);
        }
        return list;
    }

    private static string? Child(XElement e, string localName) =>
        e.Elements().FirstOrDefault(x => x.Name.LocalName == localName && x.Name.Namespace != Atom)?.Value;

    private static void Add(List<NewsItem> list, NewsFeed feed, string title, string? href, DateTimeOffset when)
    {
        if (title.Length == 0 || href is null) return;
        if (!Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        // Feeds occasionally carry a post-dated item (scheduled publishing); clamp so it does not sit on top for hours.
        var now = DateTimeOffset.UtcNow;
        if (when > now.AddMinutes(5)) when = now;
        list.Add(new NewsItem(feed.Id, feed.Name, title, uri, when));
    }

    /// <summary>RFC 822/1123 (RSS), ISO 8601 (Atom, dc:date) and a few sloppy variants.</summary>
    public static DateTimeOffset? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        // Common zone names .NET does not accept in RFC 822 strings.
        s = ZoneWord().Replace(s, m => m.Value.ToUpperInvariant() switch
        {
            "UT" or "GMT" or "UTC" or "Z" => "+0000",
            "EST" => "-0500", "EDT" => "-0400", "CST" => "-0600", "CDT" => "-0500",
            "MST" => "-0700", "MDT" => "-0600", "PST" => "-0800", "PDT" => "-0700",
            _ => m.Value
        });
        return DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var d)
            ? d : null;
    }

    [GeneratedRegex(@"(?<=\s)(UT|GMT|UTC|Z|EST|EDT|CST|CDT|MST|MDT|PST|PDT)$", RegexOptions.IgnoreCase)]
    private static partial Regex ZoneWord();

    /// <summary>Entity-decode, strip any markup, collapse whitespace.</summary>
    public static string Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = WebUtility.HtmlDecode(s);
        t = Tags().Replace(t, "");
        t = WebUtility.HtmlDecode(t);   // CDATA-wrapped titles are sometimes double-encoded
        return Spaces().Replace(t, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();

    public void Dispose()
    {
        _cts.Cancel();
        _timer?.Dispose();
        _http.Dispose();
    }
}
