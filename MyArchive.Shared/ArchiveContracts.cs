using System.ComponentModel.DataAnnotations;

namespace MyArchive.Shared;

public enum ArchiveGroupBy
{
    None,
    Type,
    Status,
    Tag
}

public enum ImportFormat
{
    Json,
    Csv
}

public sealed class ArchiveQuery
{
    public string Search { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public ItemStatus? Status { get; set; }
    public ArchiveGroupBy GroupBy { get; set; } = ArchiveGroupBy.Type;
}

public sealed record ArchiveListItemSummary(
    Guid Id,
    string Title,
    string Description,
    string Type,
    ItemStatus Status,
    ItemPriority Priority,
    double ProgressPercent,
    string ProgressLabel,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string Notes,
    IReadOnlyList<string> Tags,
    int ChecklistCompleted,
    int ChecklistTotal);

public sealed record ArchiveGroup(string Title, IReadOnlyList<ArchiveListItemSummary> Items);

public sealed class ArchiveCatalog
{
    public required IReadOnlyList<ArchiveListItemSummary> Items { get; init; }
    public required IReadOnlyList<ArchiveGroup> Groups { get; init; }
    public required IReadOnlyList<string> AvailableTypes { get; init; }
    public required IReadOnlyList<string> AvailableTags { get; init; }
}

public sealed record DashboardMetric(string Label, string Value, string Accent);

public sealed record DashboardTimelinePoint(string Label, int Value);

public sealed class DashboardSummary
{
    public required IReadOnlyList<DashboardMetric> Metrics { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> InProgress { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> RecentlyCompleted { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> PriorityFocus { get; init; }
    public required IReadOnlyList<DashboardTimelinePoint> StatusBreakdown { get; init; }
    public required IReadOnlyList<DashboardTimelinePoint> TypeBreakdown { get; init; }
}

public sealed class ItemEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Titulo e obrigatorio.")]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tipo e obrigatorio.")]
    [StringLength(80)]
    public string Type { get; set; } = "Outro";

    [Required]
    public ItemStatus Status { get; set; } = ItemStatus.NotStarted;

    [Required]
    public ItemPriority Priority { get; set; } = ItemPriority.Medium;

    [Range(0, 100)]
    public double ProgressPercent { get; set; }

    [StringLength(120)]
    public string ProgressLabel { get; set; } = string.Empty;

    [StringLength(12000)]
    public string Notes { get; set; } = string.Empty;

    public string TagsText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? CompletedAt { get; set; }

    public List<ChecklistEntryModel> ChecklistEntries { get; set; } = [];
}

public sealed class ChecklistEntryModel
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public sealed record ImportSummary(int Created, int Updated, int TotalProcessed, string Message);
