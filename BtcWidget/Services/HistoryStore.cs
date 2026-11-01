using System.IO;
using BtcWidget.Models;
using Microsoft.Data.Sqlite;

namespace BtcWidget.Services;

/// <summary>SQLite-backed price history. One row per (source, symbol, timestamp).</summary>
public sealed class HistoryStore : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _gate = new();

    public HistoryStore(string? path = null)
    {
        path ??= Path.Combine(SettingsStore.DataDirectory, "history.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS prices (
                source TEXT NOT NULL,
                symbol TEXT NOT NULL,
                ts     INTEGER NOT NULL,
                price  REAL NOT NULL,
                PRIMARY KEY (source, symbol, ts)
            ) WITHOUT ROWID;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Insert(PricePoint p)
    {
        lock (_gate) InsertUnlocked(p);
    }

    public void InsertMany(IEnumerable<PricePoint> points)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            foreach (var p in points) InsertUnlocked(p);
            tx.Commit();
        }
    }

    private void InsertUnlocked(PricePoint p)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO prices(source, symbol, ts, price) VALUES ($s, $y, $t, $p)";
        cmd.Parameters.AddWithValue("$s", p.Source);
        cmd.Parameters.AddWithValue("$y", p.Symbol);
        cmd.Parameters.AddWithValue("$t", p.Timestamp.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$p", (double)p.Price);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Price closest to (and not after) the given instant, within <paramref name="tolerance"/>.</summary>
    public decimal? PriceAt(string source, string symbol, DateTimeOffset at, TimeSpan tolerance)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT price FROM prices
                WHERE source = $s AND symbol = $y AND ts <= $t AND ts >= $lo
                ORDER BY ts DESC LIMIT 1
                """;
            cmd.Parameters.AddWithValue("$s", source);
            cmd.Parameters.AddWithValue("$y", symbol);
            cmd.Parameters.AddWithValue("$t", at.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$lo", (at - tolerance).ToUnixTimeSeconds());
            var r = cmd.ExecuteScalar();
            return r is double d ? (decimal)d : null;
        }
    }

    public DateTimeOffset? OldestTimestamp(string source, string symbol)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT MIN(ts) FROM prices WHERE source = $s AND symbol = $y";
            cmd.Parameters.AddWithValue("$s", source);
            cmd.Parameters.AddWithValue("$y", symbol);
            var r = cmd.ExecuteScalar();
            return r is long l ? DateTimeOffset.FromUnixTimeSeconds(l) : null;
        }
    }

    public IReadOnlyList<PricePoint> Recent(string source, string symbol, TimeSpan span)
    {
        lock (_gate)
        {
            var list = new List<PricePoint>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT ts, price FROM prices WHERE source = $s AND symbol = $y AND ts >= $lo ORDER BY ts";
            cmd.Parameters.AddWithValue("$s", source);
            cmd.Parameters.AddWithValue("$y", symbol);
            cmd.Parameters.AddWithValue("$lo", (DateTimeOffset.UtcNow - span).ToUnixTimeSeconds());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new PricePoint(source, symbol, DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)), (decimal)reader.GetDouble(1)));
            return list;
        }
    }

    public void Dispose() => _conn.Dispose();
}
