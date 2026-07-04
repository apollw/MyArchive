using System.ComponentModel.DataAnnotations;

namespace MyArchive.Shared;

public enum ArchiveGroupBy
{
    None,
    Category,
    Status
}

public enum ImportFormat
{
    Json,
    Csv
}

public sealed class ArchiveQuery
{
    public string Search { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ArchiveGroupBy GroupBy { get; set; } = ArchiveGroupBy.Category;
}

public sealed record ArchiveListItemSummary(
    Guid Id,
    string Title,
    string Category,
    string Status,
    string CoverImageUrl,
    string CoverThumbnailUrl,
    double? Rating,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string Review,
    string Notes,
    IReadOnlyList<string> Tags,
    string? GameMedia,
    string? GamePlatform,
    bool FinishedOnAnotherPlatform,
    int EpisodeCount,
    int CompletedEpisodeCount);

public sealed record ArchiveGroup(string Title, IReadOnlyList<ArchiveListItemSummary> Items);

public sealed class ArchiveCatalog
{
    public required IReadOnlyList<ArchiveListItemSummary> Items { get; init; }
    public required IReadOnlyList<ArchiveGroup> Groups { get; init; }
    public required IReadOnlyList<string> AvailableCategories { get; init; }
    public required IReadOnlyList<string> AvailableStatuses { get; init; }
}

public sealed record DashboardMetric(string Label, string Value, string Accent);

public sealed record DashboardTimelinePoint(string Label, int Value);

public sealed class DashboardSummary
{
    public required IReadOnlyList<DashboardMetric> Metrics { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> LatestItems { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> ActiveItems { get; init; }
    public required IReadOnlyList<ArchiveListItemSummary> RecentlyCompleted { get; init; }
    public required IReadOnlyList<DashboardTimelinePoint> StatusBreakdown { get; init; }
    public required IReadOnlyList<DashboardTimelinePoint> CategoryBreakdown { get; init; }
}

public sealed class ItemEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Titulo e obrigatorio.")]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Capa e obrigatoria.")]
    public string CoverImageUrl { get; set; } = ArchiveMetadata.DefaultCoverPath;

    public string CoverThumbnailUrl { get; set; } = ArchiveMetadata.DefaultCoverPath;

    [Required(ErrorMessage = "Categoria e obrigatoria.")]
    [StringLength(80)]
    public string Category { get; set; } = ArchiveMetadata.Categories.First();

    [Required(ErrorMessage = "Status e obrigatorio.")]
    [StringLength(80)]
    public string Status { get; set; } = string.Empty;

    [Range(0, 10, ErrorMessage = "A nota deve ficar entre 0 e 10.")]
    public double? Rating { get; set; }

    [StringLength(20000)]
    public string Review { get; set; } = string.Empty;

    [StringLength(12000)]
    public string Notes { get; set; } = string.Empty;

    public string TagsText { get; set; } = string.Empty;

    [StringLength(40)]
    public string GameMedia { get; set; } = string.Empty;

    [StringLength(60)]
    public string GamePlatform { get; set; } = string.Empty;

    public bool FinishedOnAnotherPlatform { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? CompletedAt { get; set; }

    public List<EpisodeEditorModel> Episodes { get; set; } = [];
}

public sealed class EpisodeEditorModel
{
    public Guid? Id { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int SortOrder { get; set; }
}

public sealed record ImportSummary(int Created, int Updated, int TotalProcessed, string Message);