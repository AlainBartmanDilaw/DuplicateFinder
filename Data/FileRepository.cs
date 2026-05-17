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
                Id     TEXT PRIMARY KEY,
                Sha256 TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Fichier (
                Id            TEXT PRIMARY KEY,
                FullPath      TEXT NOT NULL,
                FileSize      INTEGER NOT NULL,
                CrcId         TEXT NOT NULL,
                LastWriteTime TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_fichier_path  ON Fichier(FullPath);
            CREATE INDEX        IF NOT EXISTS idx_fichier_crcid ON Fichier(CrcId);
        ");

        // Migration : ajoute LastWriteTime si absente (bases créées avant cette colonne)
        var cols = new HashSet<string>();
        using var pragma = _db.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(Fichier)";
        using var pr = pragma.ExecuteReader();
        while (pr.Read()) cols.Add(pr.GetString(1));
        if (!cols.Contains("LastWriteTime"))
            Exec("ALTER TABLE Fichier ADD COLUMN LastWriteTime TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000'");
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

    public FileEntry? FindFileByPath(string path)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id, FullPath, FileSize, CrcId, LastWriteTime FROM Fichier WHERE FullPath = $path";
        cmd.Parameters.AddWithValue("$path", path);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadFileEntry(r) : null;
    }

    public void UpsertFile(FileEntry file)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Fichier (Id, FullPath, FileSize, CrcId, LastWriteTime)
            VALUES ($id, $path, $size, $crc, $lwt)
            ON CONFLICT(FullPath) DO UPDATE SET
                FileSize      = excluded.FileSize,
                CrcId         = excluded.CrcId,
                LastWriteTime = excluded.LastWriteTime";
        cmd.Parameters.AddWithValue("$id",   file.Id.ToString());
        cmd.Parameters.AddWithValue("$path", file.FullPath);
        cmd.Parameters.AddWithValue("$size", file.FileSize);
        cmd.Parameters.AddWithValue("$crc",  file.CrcId.ToString());
        cmd.Parameters.AddWithValue("$lwt",  file.LastWriteTime.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<FileEntry> GetAllFiles()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT f.Id, f.FullPath, f.FileSize, f.CrcId, f.LastWriteTime, c.Sha256
            FROM Fichier f
            JOIN Crc c ON c.Id = f.CrcId";
        using var r = cmd.ExecuteReader();
        var list = new List<FileEntry>();
        while (r.Read())
        {
            var entry = ReadFileEntry(r);
            entry.Sha256 = r.GetString(5);
            list.Add(entry);
        }
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
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT f.Id, f.FullPath, f.FileSize, f.CrcId, f.LastWriteTime, c.Sha256
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
            var sha   = r.GetString(5);
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
        Id            = Guid.Parse(r.GetString(0)),
        FullPath      = r.GetString(1),
        FileSize      = r.GetInt64(2),
        CrcId         = Guid.Parse(r.GetString(3)),
        LastWriteTime = DateTime.Parse(r.GetString(4))
    };

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
