using System.Security.Cryptography;
using DuplicateFinder.Data;
using DuplicateFinder.Models;

namespace DuplicateFinder.Services;

public class ScanOptions
{
    public string[] Extensions { get; set; } = [];   // vide = tout
    public bool Recursive { get; set; } = true;
}

public class ScanProgress
{
    public int Total { get; set; }
    public int Done { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public int Percent => Total == 0 ? 0 : (int)(Done * 100.0 / Total);
}

public sealed class DirectoryScanner
{
    private readonly FileRepository _repo;

    public DirectoryScanner(FileRepository repo) => _repo = repo;

    /// <summary>
    /// Scanne <paramref name="rootPath"/> et ses sous-répertoires.
    /// Notifie la progression via <paramref name="progress"/> (appelé depuis le thread de scan).
    /// </summary>
    public async Task ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var searchOption = options.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory
            .EnumerateFiles(rootPath, "*.*", searchOption)
            .Where(f => PassesFilter(f, options.Extensions))
            .ToList();

        var report = new ScanProgress { Total = files.Count };

        await Task.Run(() =>
        {
            foreach (var filePath in files)
            {
                ct.ThrowIfCancellationRequested();

                report.CurrentFile = filePath;
                progress?.Report(report);

                try
                {
                    var info = new FileInfo(filePath);
                    var existing = _repo.FindFileByPath(filePath);

                    if (existing != null && existing.LastWriteTime == info.LastWriteTime)
                    {
                        report.Done++;
                        progress?.Report(report);
                        continue;
                    }

                    var sha = ComputeSha256(filePath);
                    var crc = _repo.GetOrCreateCrc(sha);
                    var entry = new FileEntry
                    {
                        Id            = existing?.Id ?? Guid.NewGuid(),
                        FullPath      = filePath,
                        FileSize      = info.Length,
                        CrcId         = crc.Id,
                        Sha256        = sha,
                        LastWriteTime = info.LastWriteTime
                    };
                    _repo.UpsertFile(entry);
                }
                catch (IOException) { /* fichier verrouillé, on passe */ }
                catch (UnauthorizedAccessException) { }

                report.Done++;
                progress?.Report(report);
            }
        }, ct);
    }

    private static bool PassesFilter(string path, string[] extensions)
    {
        if (extensions is null || extensions.Length == 0) return true;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return extensions.Contains(ext);
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
