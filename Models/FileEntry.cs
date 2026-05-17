namespace DuplicateFinder.Models;

public class FileEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid CrcId { get; set; }

    // Navigation (non persisté)
    public string? Sha256 { get; set; }
}
