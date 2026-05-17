using Microsoft.Data.Sqlite;
using DuplicateFinder.Models;

namespace DuplicateFinder.Data;

public sealed class FileRepository : IDisposable
{
    private readonly SqliteConnection _db;

    public FileRepository(string dbPath)
    {
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        Exec(@"
            CREATE TABLE IF NOT EXISTS Crc (
                Id    TEXT PRIMARY KEY,
                Sha256 TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Fichier (
                Id       TEXT PRIMARY KEY,
                FullPath TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                CrcId    TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_fichier_path  ON Fichier(FullPath);
            CREATE INDEX IF NOT EXISTS idx_fichier_crcid ON Fichier(CrcId);
        ");
    }

    // ── CRC ────────────────────────────────────────────────────────────────

    public CrcEntry GetOrCreateCrc(string sha256)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Crc WHERE Sha256 = $sha";
        cmd.Parameters.AddWithValue("$sha", sha256);
        var existing = cmd.ExecuteScalar() as string;
        if (existing != null)
            return new CrcEntry { Id = Guid.Parse(existing), Sha256 = sha256 };

        var entry = new CrcEntry { Sha256 = sha256 };
        using var ins = _db.CreateCommand();
        ins.CommandText = "INSERT INTO Crc (Id, Sha256) VALUES ($id, $sha)";
        ins.Parameters.AddWithValue("$id", entry.Id.ToString());
        ins.Parameters.AddWithValue("$sha", sha256);
        ins.ExecuteNonQuery();
        return entry;
    }

    public CrcEntry? FindCrc(string sha256)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Crc WHERE Sha256 = $sha";
        cmd.Parameters.AddWithValue("$sha", sha256);
        var id = cmd.ExecuteScalar() as string;
        return id is null ? null : new CrcEntry { Id = Guid.Parse(id), Sha256 = sha256 };
    }

    // ── Fichiers ────────────────────────────────────────────────────────────

    public void UpsertFile(FileEntry file)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Fichier (Id, FullPath, FileSize, CrcId)
            VALUES ($id, $path, $size, $crc)
            ON CONFLICT(Id) DO UPDATE SET
                FullPath = excluded.FullPath,
                FileSize = excluded.FileSize,
                CrcId    = excluded.CrcId";
        cmd.Parameters.AddWithValue("$id",   file.Id.ToString());
        cmd.Parameters.AddWithValue("$path", file.FullPath);
        cmd.Parameters.AddWithValue("$size", file.FileSize);
        cmd.Parameters.AddWithValue("$crc",  file.CrcId.ToString());
        cmd.ExecuteNonQuery();
    }

    public List<FileEntry> GetAllFiles()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id, FullPath, FileSize, CrcId FROM Fichier";
        using var r = cmd.ExecuteReader();
        var list = new List<FileEntry>();
        while (r.Read())
            list.Add(ReadFileEntry(r));
        return list;
    }

    public void DeleteFile(Guid id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Fichier WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    public void ClearAll()
    {
        Exec("DELETE FROM Fichier; DELETE FROM Crc;");
    }

    // ── Doublons ────────────────────────────────────────────────────────────

    public List<DuplicateGroup> GetDuplicateGroups()
    {
        // Récupère les CrcId ayant au moins 2 fichiers
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT f.Id, f.FullPath, f.FileSize, f.CrcId, c.Sha256
            FROM Fichier f
            JOIN Crc c ON c.Id = f.CrcId
            WHERE f.CrcId IN (
                SELECT CrcId FROM Fichier GROUP BY CrcId HAVING COUNT(*) > 1
            )
            ORDER BY f.CrcId, f.FullPath";

        using var r = cmd.ExecuteReader();
        var grouped = new Dictionary<string, (string Sha256, List<FileEntry> Files)>();

        while (r.Read())
        {
            var entry = ReadFileEntry(r);
            var sha   = r.GetString(4);
            var crcId = entry.CrcId.ToString();
            entry.Sha256 = sha;

            if (!grouped.TryGetValue(crcId, out var g))
            {
                g = (sha, []);
                grouped[crcId] = g;
            }
            g.Files.Add(entry);
        }

        return grouped.Values
            .Select(g => new DuplicateGroup { Sha256 = g.Sha256, Files = g.Files })
            .OrderByDescending(g => g.Count)
            .ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static FileEntry ReadFileEntry(SqliteDataReader r) => new()
    {
        Id       = Guid.Parse(r.GetString(0)),
        FullPath = r.GetString(1),
        FileSize = r.GetInt64(2),
        CrcId    = Guid.Parse(r.GetString(3))
    };

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
