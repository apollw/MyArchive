namespace MyArchive.Models;

public sealed class ItemChecklistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArchiveItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}
