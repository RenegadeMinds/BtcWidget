using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace BtcWidget.Services;

public sealed record UpdateInfo(Version Latest, string Url, bool IsNewer);

/// <summary>Asks GitHub for the latest release and compares its tag with this build's version.</summary>
public static class UpdateChecker
{
    public const string RepoUrl = "https://github.com/RenegadeMinds/BtcWidget";
    public const string ReleasesUrl = RepoUrl + "/releases";
    private const string LatestApi = "https://api.github.com/repos/RenegadeMinds/BtcWidget/releases/latest";

    /// <summary>This build's version, three parts ("1.4.0").</summary>
    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string CurrentText => Current.ToString(3);

    /// <summary>Set after <see cref="CheckAsync"/> returns null: why.</summary>
    public static string LastError { get; private set; } = "";

    /// <summary>Null when GitHub could not be reached or has no release yet (see <see cref="LastError"/>).</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        LastError = "";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"BtcWidget/{CurrentText} (+{RepoUrl})");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var doc = await http.GetFromJsonAsync<JsonDocument>(LatestApi, ct).ConfigureAwait(false);
            if (doc is null) return null;
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            if (!TryParse(tag, out var latest)) { LastError = $"Unrecognised release tag \"{tag}\"."; return null; }
            return new UpdateInfo(latest, url ?? ReleasesUrl, latest > Current);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            LastError = "No releases have been published on GitHub yet.";
            return null;
        }
        catch (Exception ex)
        {
            LastError = $"Could not reach GitHub ({ex.GetType().Name}). Try again later.";
            return null;
        }
    }

    /// <summary>"v1.4.0", "1.4", "release-1.4.0" → 1.4.0.</summary>
    public static bool TryParse(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var digits = tag.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray();
        var s = new string(digits).Trim('.');
        if (s.Count(c => c == '.') == 0) s += ".0";
        if (!Version.TryParse(s, out var v)) return false;
        version = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        return true;
    }
}
