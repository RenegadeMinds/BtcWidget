using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BtcWidget.Models;

namespace BtcWidget.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BtcWidget");

    public string SettingsPath { get; } = Path.Combine(DataDirectory, "settings.json");

    public AppSettings Load()
    {
        AppSettings settings = new();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt settings: fall through to defaults.
        }
        Migrate(settings);
        return settings;
    }

    /// <summary>Converts the first version's Feeds list into Lines and fills defaults for an empty file.</summary>
    private static void Migrate(AppSettings s)
    {
        if (s.Lines.Count == 0 && s.Feeds is { Count: > 0 })
        {
            foreach (var feed in s.Feeds.Where(f => f.Enabled && f.Type == FeedType.Price))
            foreach (var source in feed.Sources)
            {
                var template = LineConfig.Defaults()[0];
                s.Lines.Add(new LineConfig
                {
                    Kind = InstrumentKind.Crypto,
                    Name = feed.Symbol == "BTC" ? "Bitcoin" : feed.Symbol,
                    Symbol = feed.Symbol,
                    CoinId = feed.Symbol == "BTC" ? "bitcoin" : feed.Symbol.ToLowerInvariant(),
                    LogoUrl = feed.Symbol == "BTC" ? template.LogoUrl : null,
                    ProviderId = source
                });
            }
        }
        if (s.Lines.Count == 0 && s.Feeds is null) s.Lines = LineConfig.Defaults();
        s.Feeds = null;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}
