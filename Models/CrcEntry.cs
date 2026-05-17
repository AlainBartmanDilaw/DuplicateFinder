namespace DuplicateFinder.Models;

public class CrcEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sha256 { get; set; } = string.Empty;
}
