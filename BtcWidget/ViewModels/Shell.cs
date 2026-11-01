using System.Diagnostics;

namespace BtcWidget.ViewModels;

internal static class Shell
{
    public static void OpenUrl(Uri url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // No default browser or blocked: ignore.
        }
    }
}
