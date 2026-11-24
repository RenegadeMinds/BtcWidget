using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BtcWidget.Models;

namespace BtcWidget.Services;

/// <summary>
/// Loads the data files in <c>Data\</c> (providers.json, currencies.json): everything about external sites
/// that can change without a rebuild. The user has an editable copy in <c>%LocalAppData%\BtcWidget</c>, and
/// <c>shipped\</c> beside it remembers the version that copy was last in step with. Comparing the three tells
/// a value the user changed (kept) from one they never touched (updated), so a new version delivers its fixes
/// and the user's edits survive it.
/// </summary>
public static class DataFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Things the user should hear about after loading: a data file that could not be read.</summary>
    public static List<string> Problems { get; } = [];

    public static JsonNode Load(string fileName, string? userDirectory = null)
    {
        var directory = userDirectory ?? SettingsStore.DataDirectory;
        var userPath = Path.Combine(directory, fileName);
        var snapshotPath = Path.Combine(directory, "shipped", fileName);
        var besideExe = ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", fileName));

        JsonNode? shipped = null;
        foreach (var json in new[] { ReadEmbedded(fileName), besideExe })
            if (Parse(json) is { } node) shipped = shipped is null ? node : Merge(shipped, node);
        if (shipped is null) throw new InvalidDataException($"Data file {fileName} is missing or unreadable.");

        if (!File.Exists(userPath))
        {
            // First run: give the user a copy to edit, the shipped file verbatim (comments and layout intact).
            TryWrite(userPath, besideExe ?? ReadEmbedded(fileName));
            TryWrite(snapshotPath, shipped.ToJsonString(WriteOptions));
            return shipped;
        }

        var userText = ReadFile(userPath);
        var user = Parse(userText);
        if (user is null)
        {
            Problems.Add($"{fileName} has a mistake, so the built-in values are in use. Your file was not changed.");
            return shipped;
        }

        var baseline = Parse(ReadFile(snapshotPath)) ?? Parse(ReadEmbedded("baseline." + fileName)) ?? shipped;
        var merged = ThreeWayMerge(baseline, user, shipped) ?? shipped;
        if (!JsonNode.DeepEquals(baseline, shipped))
        {
            // A new version changed this file: bring the user's copy up to date, keeping a copy of how it was.
            TryWrite(snapshotPath + ".before-update", userText);
            if (TryWrite(userPath, merged.ToJsonString(WriteOptions)))
                TryWrite(snapshotPath, shipped.ToJsonString(WriteOptions));
        }
        else if (!File.Exists(snapshotPath))
        {
            TryWrite(snapshotPath, shipped.ToJsonString(WriteOptions));
        }
        return merged;
    }

    /// <summary>
    /// Objects merge key by key. For anything else (a value or a whole list): if the user's value is still what
    /// they were given, take the new one; if they changed it, keep theirs. Keys only the new version has are
    /// added; keys the new version dropped go too, unless the user changed or added them.
    /// </summary>
    public static JsonNode? ThreeWayMerge(JsonNode? baseline, JsonNode? user, JsonNode? shipped)
    {
        if (user is JsonObject u && shipped is JsonObject s)
        {
            var b = baseline as JsonObject;
            var result = new JsonObject();
            foreach (var (key, value) in u)
            {
                if (s.ContainsKey(key)) result[key] = ThreeWayMerge(b?[key], value, s[key])?.DeepClone();
                else if (b is null || !b.ContainsKey(key) || !JsonNode.DeepEquals(b[key], value)) result[key] = value?.DeepClone();
            }
            foreach (var (key, value) in s)
                if (!u.ContainsKey(key)) result[key] = value?.DeepClone();
            return result;
        }
        return JsonNode.DeepEquals(baseline, user) ? shipped?.DeepClone() : user?.DeepClone();
    }

    /// <summary>Copies a file that could not be read to name.broken-date.json. Null if even that failed.</summary>
    public static string? Quarantine(string path)
    {
        try
        {
            var copy = Path.Combine(Path.GetDirectoryName(path)!,
                $"{Path.GetFileNameWithoutExtension(path)}.broken-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(path)}");
            File.Copy(path, copy, overwrite: true);
            return copy;
        }
        catch { return null; }
    }

    private static JsonNode? Parse(string? json)
    {
        if (json is null) return null;
        try { return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch { return null; }
    }

    private static bool TryWrite(string path, string? text)
    {
        if (text is null) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch { return false; }   // read-only profile
    }

    public static string UserPath(string fileName) => Path.Combine(SettingsStore.DataDirectory, fileName);

    private static string? ReadFile(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; } catch { return null; }
    }

    private static string? ReadEmbedded(string fileName)
    {
        try
        {
            using var s = typeof(DataFiles).Assembly.GetManifestResourceStream("BtcWidget.Data." + fileName);
            if (s is null) return null;
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch { return null; }
    }

    /// <summary>Deep merge: objects recurse, everything else (arrays included) is replaced by the overriding value.</summary>
    private static JsonNode Merge(JsonNode? baseNode, JsonNode? over)
    {
        if (over is null) return baseNode ?? new JsonObject();
        if (baseNode is JsonObject b && over is JsonObject o)
        {
            foreach (var (key, value) in o.ToList())
                b[key] = b.ContainsKey(key) ? Merge(b[key], value?.DeepClone()) : value?.DeepClone();
            return b;
        }
        return over.DeepClone();
    }

    public static T Deserialize<T>(JsonNode node) => node.Deserialize<T>(JsonOptions) ?? throw new InvalidDataException(typeof(T).Name);
}

/// <summary>A footer link: shown for lines of the listed kinds, URL from a template.</summary>
public sealed class LinkConfig
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<InstrumentKind> Kinds { get; set; } = [];
    public string Url { get; set; } = "";
    public string IconUri => Providers.IconUri(Icon);
}

/// <summary>One starter line, from providers.json → defaultLines.</summary>
public sealed class DefaultLine
{
    public InstrumentKind Kind { get; set; } = InstrumentKind.Crypto;
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string? CoinId { get; set; }
    public string Provider { get; set; } = "";
    public string? Logo { get; set; }

    public LineConfig ToLine() => new()
    {
        Kind = Kind, Name = Name, Symbol = Symbol, CoinId = CoinId, ProviderId = Provider, LogoUrl = Logo
    };
}

/// <summary>One spot metal, from providers.json → metals.</summary>
public sealed class MetalConfig
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Kitco { get; set; }
    public string? Futures { get; set; }
    public List<string> Providers { get; set; } = [];
}

/// <summary>One provider's entry in providers.json.</summary>
public sealed class ProviderConfig
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Page { get; set; } = "";
    public Dictionary<string, string> Api { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string IconUri => Providers.IconUri(Icon);

    /// <summary>The API endpoint named <paramref name="key"/>, with placeholders filled. Missing key = a clear error.</summary>
    public string ApiUrl(string key, params (string Key, string Value)[] values)
    {
        if (!Api.TryGetValue(key, out var template))
            throw new InvalidOperationException($"providers.json: no api.{key} for {Name}. Restore the file or add the entry.");
        return UrlTemplate.Fill(template, values);
    }

    public Uri PageUrl(LineConfig line, params (string Key, string Value)[] extra) => new(UrlTemplate.Fill(Page, line, extra));
}

/// <summary>Registry of the loaded provider data. <see cref="Load"/> runs once at startup.</summary>
public static class Providers
{
    private static Dictionary<string, ProviderConfig> _providers = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<LinkConfig> Links { get; private set; } = [];
    public static IReadOnlyList<DefaultLine> DefaultLines { get; private set; } = [];
    public static IReadOnlyList<MetalConfig> Metals { get; private set; } = [];

    public static void Load(string? userDirectory = null)
    {
        var root = DataFiles.Load("providers.json", userDirectory);
        _providers = DataFiles.Deserialize<Dictionary<string, ProviderConfig>>(root["providers"] ?? new JsonObject());
        _providers = new Dictionary<string, ProviderConfig>(_providers, StringComparer.OrdinalIgnoreCase);
        Links = root["links"] is JsonNode l ? DataFiles.Deserialize<List<LinkConfig>>(l) : [];
        DefaultLines = root["defaultLines"] is JsonNode d ? DataFiles.Deserialize<List<DefaultLine>>(d) : [];
        Metals = root["metals"] is JsonNode m ? DataFiles.Deserialize<List<MetalConfig>>(m) : [];
    }

    public static ProviderConfig Get(string id)
    {
        if (_providers.Count == 0) Load();
        return _providers.TryGetValue(id, out var p) ? p
            : throw new InvalidOperationException($"providers.json has no entry for '{id}'.");
    }

    /// <summary>"kraken.png" → the pack URI of that asset; an absolute URI is returned as is.</summary>
    public static string IconUri(string icon) =>
        icon.Contains("://") ? icon : $"pack://application:,,,/Assets/{icon}";
}

/// <summary>Quote list and fiat symbols from currencies.json.</summary>
public static class Currencies
{
    private static string[] _quotes = ["USD"];
    private static Dictionary<string, string> _fiat = new(StringComparer.OrdinalIgnoreCase);

    public static void Load(string? userDirectory = null)
    {
        var root = DataFiles.Load("currencies.json", userDirectory);
        _quotes = root["quotes"] is JsonNode q ? DataFiles.Deserialize<string[]>(q) : ["USD"];
        _fiat = root["fiat"] is JsonNode f
            ? new Dictionary<string, string>(DataFiles.Deserialize<Dictionary<string, string>>(f), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["USD"] = "$" };
    }

    public static IReadOnlyList<string> Quotes { get { if (_fiat.Count == 0) Load(); return _quotes; } }
    public static bool IsFiat(string code) { if (_fiat.Count == 0) Load(); return _fiat.ContainsKey(code); }
    public static string? Prefix(string code) { if (_fiat.Count == 0) Load(); return _fiat.TryGetValue(code, out var p) ? p : null; }
}

/// <summary>Fills {placeholders} in a URL template; values are URL-escaped. Unknown placeholders are left alone.</summary>
public static class UrlTemplate
{
    public static string Fill(string template, params (string Key, string Value)[] values)
    {
        var s = template;
        foreach (var (key, value) in values) s = s.Replace("{" + key + "}", Uri.EscapeDataString(value));
        return s;
    }

    /// <summary>The standard placeholders for a line, plus any extras (extras win).</summary>
    public static string Fill(string template, LineConfig line, params (string Key, string Value)[] extra)
    {
        var quote = line.EffectiveQuote.Length > 0 ? line.EffectiveQuote : "USD";
        var standard = new (string, string)[]
        {
            ("symbol", line.Symbol), ("base", line.Symbol.ToLowerInvariant()), ("BASE", line.Symbol.ToUpperInvariant()),
            ("quote", quote.ToLowerInvariant()), ("QUOTE", quote.ToUpperInvariant()),
            ("coinId", line.CoinId ?? line.Symbol.ToLowerInvariant()), ("product", $"{line.Symbol.ToUpperInvariant()}-{quote.ToUpperInvariant()}"),
            ("nasdaqClass", line.Kind == InstrumentKind.Etf ? "etf" : "stocks"), ("symbolNoCaret", line.Symbol.TrimStart('^'))
        };
        return Fill(template, extra.Concat(standard).ToArray());
    }
}
