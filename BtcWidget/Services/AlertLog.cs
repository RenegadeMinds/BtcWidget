using System.IO;

namespace BtcWidget.Services;

/// <summary>
/// Append-only record of fired alerts: %LocalAppData%\BtcWidget\alerts.log, one line each.
/// Useful for "what happened while I was away" and for checking that notifications actually went out.
/// </summary>
public static class AlertLog
{
    public static string Path { get; } = System.IO.Path.Combine(SettingsStore.DataDirectory, "alerts.log");

    public static void Append(string message, bool notified)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            File.AppendAllText(Path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}  {message}  [{(notified ? "notified" : "not notified")}]{Environment.NewLine}",
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch
        {
            // Logging must never take the widget down.
        }
    }
}
