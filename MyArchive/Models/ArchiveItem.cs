using System.ComponentModel.DataAnnotations;
using MyArchive.Shared;

namespace MyArchive.Models;

public sealed class ArchiveItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Outro";

    public ItemStatus Status { get; set; } = ItemStatus.NotStarted;

    public ItemPriority Priority { get; set; } = ItemPriority.Medium;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public double ProgressPercent { get; set; }

    public string? ProgressLabel { get; set; }

    public string? Notes { get; set; }

    public List<ItemTag> Tags { get; set; } = [];

    public List<ItemChecklistEntry> ChecklistEntries { get; set; } = [];
}
