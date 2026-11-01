using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>
/// The user's feed list: <c>%LocalAppData%\BtcWidget\feeds.json</c>. Seeded on first run from the
/// <c>Data\feeds.json</c> shipped next to the exe, then owned by the user: edit it by hand or through
/// Settings → News. <see cref="RestoreDefaults"/> merges the shipped list back in by id without touching
/// feeds the user changed or removed.
/// </summary>
public sealed class FeedStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class FeedFile
    {
        [JsonPropertyName("_comment")] public string? Comment { get; set; }
        [JsonPropertyName("feeds")] public List<NewsFeed> Feeds { get; set; } = [];
    }

    public string Path { get; } = System.IO.Path.Combine(SettingsStore.DataDirectory, "feeds.json");
    public static string DefaultsPath { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "feeds.json");

    /// <summary>The user's list; the shipped defaults when there is none yet (and they are written out).</summary>
    public List<NewsFeed> Load()
    {
        var feeds = Read(Path);
        if (feeds is null)
        {
            feeds = Defaults();
            try { Save(feeds); } catch { /* read-only profile etc. */ }
        }
        return feeds;
    }

    /// <summary>
    /// The list shipped with this build: <c>Data\feeds.json</c> beside the exe when present (edit it there for
    /// a custom build), else the copy embedded in the assembly (single-file publish). Empty only if both fail.
    /// </summary>
    public static List<NewsFeed> Defaults() => Read(DefaultsPath) ?? ReadEmbedded() ?? [];

    private static List<NewsFeed>? ReadEmbedded()
    {
        try
        {
            using var stream = typeof(FeedStore).Assembly.GetManifestResourceStream("BtcWidget.Data.feeds.json");
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            var file = JsonSerializer.Deserialize<FeedFile>(reader.ReadToEnd(), JsonOptions);
            return file?.Feeds.Where(f => !string.IsNullOrWhiteSpace(f.Url)).ToList();
        }
        catch { return null; }
    }

    private static List<NewsFeed>? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var file = JsonSerializer.Deserialize<FeedFile>(File.ReadAllText(path), JsonOptions);
            if (file is null) return null;
            // Tolerate hand edits: drop entries without a URL, invent ids where missing, de-duplicate ids.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<NewsFeed>();
            foreach (var f in file.Feeds)
            {
                if (string.IsNullOrWhiteSpace(f.Url)) continue;
                if (string.IsNullOrWhiteSpace(f.Id)) f.Id = IdFor(f.Url);
                if (!seen.Add(f.Id)) continue;
                if (string.IsNullOrWhiteSpace(f.Name)) f.Name = f.Host;
                if (string.IsNullOrWhiteSpace(f.Sector)) f.Sector = "other";
                if (string.IsNullOrWhiteSpace(f.Country)) f.Country = "INT";
                f.Sector = f.Sector.Trim().ToLowerInvariant();
                f.Country = f.Country.Trim().ToUpperInvariant();
                list.Add(f);
            }
            return list;
        }
        catch
        {
            return null;   // corrupt file: caller falls back to defaults without overwriting it… see Save
        }
    }

    public void Save(IEnumerable<NewsFeed> feeds)
    {
        Directory.CreateDirectory(SettingsStore.DataDirectory);
        var file = new FeedFile
        {
            Comment = "Your news feeds. Add, remove, rename, reorder, or set \"enabled\" / \"color\" (#RRGGBB). Settings > News edits this file too.",
            Feeds = feeds.ToList()
        };
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, JsonOptions));
        File.Move(tmp, Path, overwrite: true);
    }

    /// <summary>Adds every shipped feed whose id is not in <paramref name="feeds"/>. Returns how many were added.</summary>
    public static int RestoreDefaults(List<NewsFeed> feeds)
    {
        var have = feeds.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var d in Defaults().Where(d => !have.Contains(d.Id)))
        {
            feeds.Add(d);
            added++;
        }
        return added;
    }

    /// <summary>A readable id from a URL: "feeds.example.com-news".</summary>
    public static string IdFor(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return Guid.NewGuid().ToString("N")[..8];
        var path = u.AbsolutePath.Trim('/').Replace('/', '-');
        var id = (u.Host.Replace("www.", "") + (path.Length > 0 ? "-" + path : "")).ToLowerInvariant();
        return id.Length > 48 ? id[..48] : id;
    }
}
