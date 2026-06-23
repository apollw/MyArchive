namespace MyArchive.Models;

public sealed class ItemTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArchiveItemId { get; set; }
    public string Name { get; set; } = string.Empty;
}
