using Microsoft.Win32;

namespace BtcWidget.Services;

/// <summary>
/// "Start with Windows": a value under HKCU\...\Run pointing at this exe. The registry is the truth; the
/// Settings checkbox reads it on startup and rewrites the path if the exe has moved since.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BtcWidget";

    private static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch { return false; }
    }

    /// <summary>Returns false when the registry could not be written (locked-down profile).</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;
            if (enabled && ExePath.Length > 0) key.SetValue(ValueName, $"\"{ExePath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }

    /// <summary>If enabled but pointing somewhere else (the exe moved), point it here again.</summary>
    public static void RepairPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && ExePath.Length > 0 && !current.Trim('"').Equals(ExePath, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, $"\"{ExePath}\"");
        }
        catch { }
    }
}
