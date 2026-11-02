using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace BtcWidget.Services;

/// <summary>
/// Downloads each web image (coin logos) once into <c>%LocalAppData%\BtcWidget\cache</c> and serves the local
/// file from then on. <see cref="Local"/> answers at once: the cached path, or null while the first download
/// runs, in which case <see cref="Downloaded"/> fires (on the UI thread) when the file is ready so the view
/// model can re-bind. Pack / file URIs pass straight through.
/// </summary>
public static class ImageCache
{
    public static string Directory { get; } = Path.Combine(SettingsStore.DataDirectory, "cache");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly HashSet<string> InFlight = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Failed = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    /// <summary>Raised on the UI thread with the original URL once its file is in the cache.</summary>
    public static event Action<string>? Downloaded;

    static ImageCache()
    {
        Http.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) BtcWidget/1.4");
    }

    /// <summary>Local path (or the URL itself for pack:/file: URIs), or null while downloading / after a failure.</summary>
    public static string? Local(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        var path = PathFor(url);
        if (File.Exists(path)) return path;
        lock (Gate)
        {
            if (Failed.Contains(url) || !InFlight.Add(url)) return null;
        }
        _ = DownloadAsync(url, path);
        return null;
    }

    private static async Task DownloadAsync(string url, string path)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length == 0) throw new InvalidDataException("empty");
            System.IO.Directory.CreateDirectory(Directory);
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Downloaded?.Invoke(url));
        }
        catch
        {
            lock (Gate) Failed.Add(url);   // don't hammer a dead URL; retried next launch
        }
        finally
        {
            lock (Gate) InFlight.Remove(url);
        }
    }

    private static string PathFor(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24].ToLowerInvariant();
        var ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if (ext.Length is 0 or > 5) ext = ".img";
        return Path.Combine(Directory, hash + ext);
    }
}
