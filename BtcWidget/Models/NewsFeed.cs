using System.Text.Json.Serialization;

namespace BtcWidget.Models;

/// <summary>
/// An RSS / Atom feed for the news popup. The list lives in <c>%LocalAppData%\BtcWidget\feeds.json</c>
/// (see <see cref="Services.FeedStore"/>); nothing about feeds is hardcoded.
/// </summary>
public sealed class NewsFeed
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    /// <summary>What the feed covers: crypto, stocks, etfs, macro, commodities, forex, or anything the user likes.</summary>
    [JsonPropertyName("sector")] public string Sector { get; set; } = "other";
    /// <summary>ISO country code of the outlet ("KR", "JP"); "INT" for international.</summary>
    [JsonPropertyName("country")] public string Country { get; set; } = "INT";
    /// <summary>Language when not English ("ko", "ja", "de"). Null = English.</summary>
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    /// <summary>Home page, for the headline menu's "Visit …". Defaults to the feed URL's host.</summary>
    [JsonPropertyName("site")] public string? Site { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    /// <summary>Optional badge colour, "#RRGGBB". Null = picked automatically.</summary>
    [JsonPropertyName("color")] public string? Color { get; set; }

    /// <summary>"KR", "KR · ko": what the Settings row shows after the name.</summary>
    [JsonIgnore] public string Tag => Lang is null ? Country : $"{Country} · {Lang}";

    public static readonly string[] KnownSectors = ["crypto", "stocks", "etfs", "macro", "commodities", "forex"];

    /// <summary>Display label for a sector id.</summary>
    public static string SectorLabel(string sector) => sector.ToLowerInvariant() switch
    {
        "crypto" => "Crypto",
        "stocks" => "Stocks & markets",
        "etfs" => "ETFs",
        "macro" => "Macro & central banks",
        "commodities" => "Commodities",
        "forex" => "Forex",
        "" or "other" => "Other",
        var s => char.ToUpperInvariant(s[0]) + s[1..]
    };

    [JsonIgnore] public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";

    public Uri? SiteUri()
    {
        if (Site is not null && Uri.TryCreate(Site, UriKind.Absolute, out var site)) return site;
        return Uri.TryCreate(Url, UriKind.Absolute, out var u) ? new Uri(u.GetLeftPart(UriPartial.Authority)) : null;
    }
}
