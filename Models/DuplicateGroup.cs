namespace DuplicateFinder.Models;

/// <summary>
/// Groupe de fichiers partageant le même SHA256
/// </summary>
public class DuplicateGroup
{
    public string Sha256 { get; set; } = string.Empty;
    public List<FileEntry> Files { get; set; } = [];

    /// <summary>Le fichier référent (premier de la liste, conservé par défaut)</summary>
    public FileEntry Reference => Files[0];

    public int Count => Files.Count;
}
