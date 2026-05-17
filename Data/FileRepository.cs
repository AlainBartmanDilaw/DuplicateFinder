using LiteDB;
using DuplicateFinder.Models;

namespace DuplicateFinder.Data;

public sealed class FileRepository : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<CrcEntry> _crcCol;
    private readonly ILiteCollection<FileEntry> _fileCol;

    public FileRepository(string dbPath)
    {
        _db = new LiteDatabase(dbPath);

        _crcCol = _db.GetCollection<CrcEntry>("Crc");
        _crcCol.EnsureIndex(x => x.Sha256, unique: true);

        _fileCol = _db.GetCollection<FileEntry>("Fichier");
        _fileCol.EnsureIndex(x => x.FullPath);
        _fileCol.EnsureIndex(x => x.CrcId);
    }

    // ── CRC ────────────────────────────────────────────────────────────────

    public CrcEntry GetOrCreateCrc(string sha256)
    {
        var existing = _crcCol.FindOne(x => x.Sha256 == sha256);
        if (existing != null) return existing;

        var entry = new CrcEntry { Sha256 = sha256 };
        _crcCol.Insert(entry);
        return entry;
    }

    public CrcEntry? FindCrc(string sha256) =>
        _crcCol.FindOne(x => x.Sha256 == sha256);

    // ── Fichiers ────────────────────────────────────────────────────────────

    public void UpsertFile(FileEntry file)
    {
        var existing = _fileCol.FindOne(x => x.FullPath == file.FullPath);
        if (existing != null)
        {
            file.Id = existing.Id;
            _fileCol.Update(file);
        }
        else
        {
            _fileCol.Insert(file);
        }
    }

    public List<FileEntry> GetAllFiles() => _fileCol.FindAll().ToList();

    public void DeleteFile(Guid id) => _fileCol.Delete(id);

    public void ClearAll()
    {
        _fileCol.DeleteAll();
        _crcCol.DeleteAll();
    }

    // ── Doublons ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne les groupes de fichiers ayant le même CrcId (donc même SHA256)
    /// et contenant au moins 2 fichiers.
    /// </summary>
    public List<DuplicateGroup> GetDuplicateGroups()
    {
        var allCrcs = _crcCol.FindAll().ToDictionary(c => c.Id, c => c.Sha256);
        var allFiles = _fileCol.FindAll().ToList();

        // Enrichir avec SHA256
        foreach (var f in allFiles)
        {
            if (allCrcs.TryGetValue(f.CrcId, out var sha))
                f.Sha256 = sha;
        }

        return allFiles
            .GroupBy(f => f.CrcId)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroup
            {
                Sha256 = allCrcs.GetValueOrDefault(g.Key, "?"),
                Files = g.ToList()
            })
            .OrderByDescending(g => g.Count)
            .ToList();
    }

    public void Dispose() => _db.Dispose();
}
