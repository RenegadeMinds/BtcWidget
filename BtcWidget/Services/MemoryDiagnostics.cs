using System.Diagnostics;
using System.IO;
using System.Text;

namespace BtcWidget.Services;

/// <summary>
/// Opt-in memory snapshots for chasing leaks: set the environment variable <c>BTCWIDGET_DIAG=1</c> and the app
/// appends a block per snapshot to %LocalAppData%\BtcWidget\diag.log: process private bytes and working set,
/// managed heap size and committed, and which assemblies are loaded. Costs nothing when the variable is unset.
/// </summary>
public static class MemoryDiagnostics
{
    public static bool Enabled { get; } = Environment.GetEnvironmentVariable("BTCWIDGET_DIAG") is { Length: > 0 };
    private static string LogPath => Path.Combine(SettingsStore.DataDirectory, "diag.log");
    private static readonly HashSet<string> Seen = [];
    private static readonly HashSet<string> SeenModules = [];

    public static void Snapshot(string label)
    {
        if (!Enabled) return;
        try
        {
            using var p = Process.GetCurrentProcess();
            var gc = GC.GetGCMemoryInfo();
            var sb = new StringBuilder();
            sb.AppendLine($"== {DateTimeOffset.Now:HH:mm:ss} {label}");
            sb.AppendLine($"   private {p.PrivateMemorySize64 / 1048576,5} MB   working set {p.WorkingSet64 / 1048576,5} MB");
            sb.AppendLine($"   managed heap {gc.HeapSizeBytes / 1048576,5} MB   committed {gc.TotalCommittedBytes / 1048576,5} MB   gen2 count {GC.CollectionCount(2)}");
            var names = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "?").OrderBy(n => n).ToList();
            var fresh = names.Where(Seen.Add).ToList();
            sb.AppendLine($"   assemblies {names.Count}" + (fresh.Count > 0 ? "   new: " + string.Join(", ", fresh) : ""));
            var mods = new List<string>();
            foreach (ProcessModule m in p.Modules) mods.Add(m.ModuleName.ToLowerInvariant());
            var freshMods = mods.Where(SeenModules.Add).ToList();
            sb.AppendLine($"   native modules {mods.Count}" + (freshMods.Count > 0 && SeenModules.Count != freshMods.Count ? "   new: " + string.Join(", ", freshMods) : ""));
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
            // diagnostics only
        }
    }
}
