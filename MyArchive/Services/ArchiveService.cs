using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyArchive.Data;
using MyArchive.Models;
using MyArchive.Shared;

namespace MyArchive.Services;

public sealed class ArchiveService(IDbContextFactory<MyArchiveDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<DashboardSummary> GetDashboardAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await db.Items
            .AsNoTracking()
            .Include(item => item.Tags)
            .Include(item => item.ChecklistEntries)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();

        var summaries = items.Select(MapSummary).ToList();
        var averageRating = summaries.Where(item => item.Rating.HasValue).Select(item => item.Rating!.Value).ToList();

        var metrics = new List<DashboardMetric>
        {
            new("Total no acervo", summaries.Count.ToString(CultureInfo.InvariantCulture), "accent-slate"),
            new("Em andamento", summaries.Count(item => ArchiveMetadata.IsInProgress(item.Category, item.Status)).ToString(CultureInfo.InvariantCulture), "accent-gold"),
            new("Concluidos", summaries.Count(item => ArchiveMetadata.IsCompleted(item.Category, item.Status)).ToString(CultureInfo.InvariantCulture), "accent-green"),
            new("Nao iniciados", summaries.Count(item => ArchiveMetadata.IsNotStarted(item.Category, item.Status)).ToString(CultureInfo.InvariantCulture), "accent-red"),
            new("Media de notas", averageRating.Count == 0 ? "-" : averageRating.Average().ToString("0.0", CultureInfo.InvariantCulture), "accent-blue")
        };

        var categoryBreakdown = ArchiveMetadata.Categories
            .Select(category => new DashboardTimelinePoint(category, summaries.Count(item => item.Category.Equals(category, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var statusBreakdown = summaries
            .GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new DashboardTimelinePoint(group.Key, group.Count()))
            .ToList();

        return new DashboardSummary
        {
            Metrics = metrics,
            LatestItems = summaries
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .ToList(),
            ActiveItems = summaries
                .Where(item => ArchiveMetadata.IsInProgress(item.Category, item.Status))
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .ToList(),
            RecentlyCompleted = summaries
                .Where(item => item.CompletedAt.HasValue)
                .OrderByDescending(item => item.CompletedAt)
                .Take(6)
                .ToList(),
            StatusBreakdown = statusBreakdown,
            CategoryBreakdown = categoryBreakdown
        };
    }

    public async Task<ArchiveCatalog> GetCatalogAsync(ArchiveQuery query)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await db.Items
            .AsNoTracking()
            .Include(item => item.Tags)
            .Include(item => item.ChecklistEntries)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Title)
            .ToListAsync();

        var summaries = items.Select(MapSummary).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            summaries = summaries.Where(item =>
                    item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Review.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            summaries = summaries.Where(item => item.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            summaries = summaries.Where(item => item.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var groups = BuildGroups(summaries, query.GroupBy);
        var availableCategories = items.Select(item => ArchiveMetadata.NormalizeCategory(item.Type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();

        var availableStatuses = summaries.Select(item => item.Status)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();

        return new ArchiveCatalog
        {
            Items = summaries,
            Groups = groups,
            AvailableCategories = availableCategories,
            AvailableStatuses = availableStatuses
        };
    }

    public async Task<ItemEditorModel> CreateEditorModelAsync()
    {
        await Task.CompletedTask;
        var category = ArchiveMetadata.Categories.First();
        return new ItemEditorModel
        {
            Category = category,
            Status = ArchiveMetadata.GetStatuses(category).First(),
            CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
            CreatedAt = DateTime.Now
        };
    }

    public async Task<ItemEditorModel?> GetItemAsync(Guid id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.Items
            .AsNoTracking()
            .Include(current => current.Tags)
            .Include(current => current.ChecklistEntries)
            .FirstOrDefaultAsync(current => current.Id == id);

        if (item is null)
        {
            return null;
        }

        var category = ArchiveMetadata.NormalizeCategory(item.Type);
        var status = ArchiveMetadata.NormalizeStatus(category, item.CatalogStatus);

        return new ItemEditorModel
        {
            Id = item.Id,
            Title = item.Title,
            CoverImageUrl = string.IsNullOrWhiteSpace(item.CoverImageUrl) ? ArchiveMetadata.DefaultCoverPath : item.CoverImageUrl,
            Category = category,
            Status = status,
            Rating = item.Rating,
            Review = item.Review ?? item.Description ?? string.Empty,
            Notes = item.Notes ?? string.Empty,
            TagsText = string.Join(", ", item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name)),
            GameMedia = item.GameMedia ?? string.Empty,
            GamePlatform = item.GamePlatform ?? string.Empty,
            FinishedOnAnotherPlatform = item.FinishedOnAnotherPlatform,
            CreatedAt = item.CreatedAt.ToLocalTime(),
            CompletedAt = item.CompletedAt?.ToLocalTime(),
            Episodes = item.ChecklistEntries
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Title)
                .Select(entry => new EpisodeEditorModel
                {
                    Id = entry.Id,
                    Title = entry.Title,
                    IsCompleted = entry.IsCompleted,
                    SortOrder = entry.SortOrder
                })
                .ToList()
        };
    }

    public async Task<Guid> SaveItemAsync(ItemEditorModel model)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var entity = model.Id.HasValue
            ? await db.Items.FirstAsync(item => item.Id == model.Id.Value)
            : new ArchiveItem();

        var category = ArchiveMetadata.NormalizeCategory(model.Category);
        var status = ArchiveMetadata.NormalizeStatus(category, model.Status);
        var isGame = ArchiveMetadata.IsGameCategory(category);

        entity.Title = model.Title.Trim();
        entity.Type = category;
        entity.CatalogStatus = status;
        entity.CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl)
            ? ArchiveMetadata.DefaultCoverPath
            : model.CoverImageUrl.Trim();
        entity.Rating = model.Rating.HasValue ? Math.Round(Math.Clamp(model.Rating.Value, 0, 10), 2) : null;
        entity.Description = model.Review.Trim();
        entity.Review = NullIfWhiteSpace(model.Review);
        entity.Notes = NullIfWhiteSpace(model.Notes);
        entity.GameMedia = isGame ? NullIfWhiteSpace(model.GameMedia) : null;
        entity.GamePlatform = isGame ? NullIfWhiteSpace(model.GamePlatform) : null;
        entity.FinishedOnAnotherPlatform = isGame && model.FinishedOnAnotherPlatform;
        entity.CreatedAt = model.Id.HasValue ? entity.CreatedAt : DateTime.SpecifyKind(model.CreatedAt, DateTimeKind.Local).ToUniversalTime();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.CompletedAt = ArchiveMetadata.IsCompleted(category, status)
            ? model.CompletedAt?.ToUniversalTime() ?? DateTime.UtcNow
            : null;

        if (!model.Id.HasValue)
        {
            db.Items.Add(entity);
        }
        else
        {
            await db.Tags.Where(tag => tag.ArchiveItemId == entity.Id).ExecuteDeleteAsync();
            await db.ChecklistEntries.Where(entry => entry.ArchiveItemId == entity.Id).ExecuteDeleteAsync();
        }

        db.Tags.AddRange(ParseTagNames(model.TagsText).Select(tag => new ItemTag
        {
            ArchiveItemId = entity.Id,
            Name = tag
        }));

        var checklist = ArchiveMetadata.IsTrackableUnitCategory(category)
            ? model.Episodes.OrderBy(entry => entry.SortOrder).Select((entry, index) => new ChecklistRecord(GetUnitTitle(category, entry, index), entry.IsCompleted))
            : [];

        db.ChecklistEntries.AddRange(checklist.Select((entry, index) => new ItemChecklistEntry
        {
            ArchiveItemId = entity.Id,
            Title = entry.Title.Trim(),
            IsCompleted = entry.IsCompleted,
            SortOrder = index
        }));

        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteItemAsync(Guid id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var entity = await db.Items.FindAsync(id);

        if (entity is null)
        {
            return;
        }

        db.Items.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<string> ExportJsonAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await LoadExportRecordsAsync(db);
        var document = new ArchiveExportDocument(DateTime.UtcNow, items);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public async Task<string> ExportCsvAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await LoadExportRecordsAsync(db);

        var builder = new StringBuilder();
        builder.AppendLine("Id,Title,Category,Status,CoverImageUrl,Rating,Review,Notes,Tags,GameMedia,GamePlatform,FinishedOnAnotherPlatform,CreatedAt,CompletedAt,Checklist");

        foreach (var item in items.OrderBy(current => current.Category).ThenBy(current => current.Title))
        {
            builder.AppendJoin(',',
                EscapeCsv(item.Id.ToString()),
                EscapeCsv(item.Title),
                EscapeCsv(item.Category),
                EscapeCsv(item.Status),
                EscapeCsv(item.CoverImageUrl),
                EscapeCsv(item.Rating?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(ToSingleLine(item.Review ?? string.Empty)),
                EscapeCsv(ToSingleLine(item.Notes ?? string.Empty)),
                EscapeCsv(string.Join('|', item.Tags)),
                EscapeCsv(item.GameMedia ?? string.Empty),
                EscapeCsv(item.GamePlatform ?? string.Empty),
                EscapeCsv(item.FinishedOnAnotherPlatform.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                EscapeCsv(item.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(string.Join('|', item.Checklist.Select(entry => $"{ToSingleLine(entry.Title)}::{entry.IsCompleted}"))));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public async Task<string> ExportMarkdownAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await LoadExportRecordsAsync(db);

        var builder = new StringBuilder();
        builder.AppendLine("# MyArchive");
        builder.AppendLine();
        builder.AppendLine($"Exportado em {DateTime.Now:dd/MM/yyyy HH:mm}");
        builder.AppendLine();

        foreach (var item in items.OrderBy(current => current.Category).ThenBy(current => current.Title))
        {
            builder.AppendLine($"## {item.Title}");
            builder.AppendLine();
            builder.AppendLine($"- Categoria: {item.Category}");
            builder.AppendLine($"- Status: {item.Status}");
            builder.AppendLine($"- Capa: {item.CoverImageUrl}");
            builder.AppendLine($"- Criado em: {item.CreatedAt:dd/MM/yyyy}");

            if (item.Rating.HasValue)
            {
                builder.AppendLine($"- Nota: {item.Rating:0.##}/10");
            }

            if (!string.IsNullOrWhiteSpace(item.GameMedia))
            {
                builder.AppendLine($"- Midia: {item.GameMedia}");
            }

            if (!string.IsNullOrWhiteSpace(item.GamePlatform))
            {
                builder.AppendLine($"- Plataforma: {item.GamePlatform}");
            }

            if (item.FinishedOnAnotherPlatform)
            {
                builder.AppendLine("- Finalizado em outra plataforma: Sim");
            }

            if (item.CompletedAt.HasValue)
            {
                builder.AppendLine($"- Concluido em: {item.CompletedAt:dd/MM/yyyy}");
            }

            if (item.Tags.Count > 0)
            {
                builder.AppendLine($"- Tags: {string.Join(", ", item.Tags)}");
            }

            if (!string.IsNullOrWhiteSpace(item.Review))
            {
                builder.AppendLine();
                builder.AppendLine("### Resenha");
                builder.AppendLine();
                builder.AppendLine(item.Review);
            }

            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                builder.AppendLine();
                builder.AppendLine("### Observacoes");
                builder.AppendLine();
                builder.AppendLine(item.Notes);
            }

            if (item.Checklist.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("### Checklist legado");
                builder.AppendLine();

                foreach (var entry in item.Checklist)
                {
                    builder.AppendLine($"- [{(entry.IsCompleted ? "x" : " ")}] {entry.Title}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public async Task<ImportSummary> ImportAsync(Stream stream, ImportFormat format)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync();

        return format switch
        {
            ImportFormat.Json => await ImportJsonAsync(content),
            ImportFormat.Csv => await ImportCsvAsync(content),
            _ => throw new InvalidOperationException("Formato de importacao invalido.")
        };
    }

    private async Task<ImportSummary> ImportJsonAsync(string content)
    {
        ArchiveExportDocument? document = null;
        List<ArchiveItemRecord>? items = null;

        try
        {
            document = JsonSerializer.Deserialize<ArchiveExportDocument>(content, JsonOptions);
            items = document?.Items;
        }
        catch (JsonException)
        {
            items = JsonSerializer.Deserialize<List<ArchiveItemRecord>>(content, JsonOptions);
        }

        if (items is null)
        {
            throw new InvalidOperationException("Arquivo JSON invalido.");
        }

        return await UpsertImportItemsAsync(items);
    }

    private async Task<ImportSummary> ImportCsvAsync(string content)
    {
        var rows = ParseCsv(content);
        if (rows.Count < 2)
        {
            throw new InvalidOperationException("Arquivo CSV sem dados.");
        }

        var created = new List<ArchiveItemRecord>();
        var header = rows[0];
        var index = header
            .Select((value, position) => new { value, position })
            .ToDictionary(item => item.value, item => item.position, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Skip(1).Where(current => current.Any(value => !string.IsNullOrWhiteSpace(value))))
        {
            var category = GetValue(row, index, "Category");
            created.Add(new ArchiveItemRecord(
                ParseGuid(GetValue(row, index, "Id")),
                GetValue(row, index, "Title"),
                string.IsNullOrWhiteSpace(category) ? GetValue(row, index, "Type") : category,
                GetValue(row, index, "Status"),
                GetValue(row, index, "CoverImageUrl"),
                ParseNullableDouble(GetValue(row, index, "Rating")),
                GetValue(row, index, "Review"),
                GetValue(row, index, "Notes"),
                SplitPipeList(GetValue(row, index, "Tags")),
                GetValue(row, index, "GameMedia"),
                GetValue(row, index, "GamePlatform"),
                ParseBool(GetValue(row, index, "FinishedOnAnotherPlatform")),
                ParseDate(GetValue(row, index, "CreatedAt")) ?? DateTime.UtcNow,
                ParseDate(GetValue(row, index, "CompletedAt")),
                SplitChecklist(GetValue(row, index, "Checklist"))));
        }

        return await UpsertImportItemsAsync(created);
    }

    private async Task<ImportSummary> UpsertImportItemsAsync(IEnumerable<ArchiveItemRecord> records)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var created = 0;
        var updated = 0;

        foreach (var record in records)
        {
            var id = record.Id == Guid.Empty ? Guid.NewGuid() : record.Id;
            var entity = await db.Items
                .Include(item => item.Tags)
                .Include(item => item.ChecklistEntries)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (entity is null)
            {
                entity = new ArchiveItem { Id = id };
                db.Items.Add(entity);
                created++;
            }
            else
            {
                updated++;
            }

            var category = ArchiveMetadata.NormalizeCategory(record.Category);
            var status = ArchiveMetadata.NormalizeStatus(category, record.Status);
            var isGame = ArchiveMetadata.IsGameCategory(category);

            entity.Title = record.Title.Trim();
            entity.Type = category;
            entity.CatalogStatus = status;
            entity.CoverImageUrl = string.IsNullOrWhiteSpace(record.CoverImageUrl) ? ArchiveMetadata.DefaultCoverPath : record.CoverImageUrl.Trim();
            entity.Rating = record.Rating.HasValue ? Math.Round(Math.Clamp(record.Rating.Value, 0, 10), 2) : null;
            entity.Description = record.Review?.Trim() ?? string.Empty;
            entity.Review = NullIfWhiteSpace(record.Review);
            entity.Notes = NullIfWhiteSpace(record.Notes);
            entity.GameMedia = isGame ? NullIfWhiteSpace(record.GameMedia) : null;
            entity.GamePlatform = isGame ? NullIfWhiteSpace(record.GamePlatform) : null;
            entity.FinishedOnAnotherPlatform = isGame && record.FinishedOnAnotherPlatform;
            entity.CreatedAt = record.CreatedAt == default ? DateTime.UtcNow : DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.CompletedAt = ArchiveMetadata.IsCompleted(category, status) ? record.CompletedAt ?? DateTime.UtcNow : null;

            ReplaceTags(entity, string.Join(", ", record.Tags));
            ReplaceChecklist(entity, record.Checklist.Select(entry => new ChecklistRecord(entry.Title, entry.IsCompleted)));
        }

        await db.SaveChangesAsync();
        return new ImportSummary(created, updated, created + updated, "Importacao concluida com sucesso.");
    }

    private static IReadOnlyList<ArchiveGroup> BuildGroups(IEnumerable<ArchiveListItemSummary> items, ArchiveGroupBy groupBy)
    {
        return groupBy switch
        {
            ArchiveGroupBy.None => [new ArchiveGroup("Todos os itens", items.OrderBy(item => item.Category).ThenBy(item => item.Title).ToList())],
            ArchiveGroupBy.Category => items
                .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .Select(group => new ArchiveGroup(group.Key, group.OrderBy(item => item.Title).ToList()))
                .ToList(),
            ArchiveGroupBy.Status => items
                .GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .Select(group => new ArchiveGroup(group.Key, group.OrderBy(item => item.Category).ThenBy(item => item.Title).ToList()))
                .ToList(),
            _ => []
        };
    }

    private static ArchiveListItemSummary MapSummary(ArchiveItem item)
    {
        var category = ArchiveMetadata.NormalizeCategory(item.Type);
        var status = ArchiveMetadata.NormalizeStatus(category, item.CatalogStatus);

        return new ArchiveListItemSummary(
            item.Id,
            item.Title,
            category,
            status,
            string.IsNullOrWhiteSpace(item.CoverImageUrl) ? ArchiveMetadata.DefaultCoverPath : item.CoverImageUrl,
            item.Rating,
            item.CreatedAt.ToLocalTime(),
            item.CompletedAt?.ToLocalTime(),
            item.Review ?? item.Description ?? string.Empty,
            item.Notes ?? string.Empty,
            item.Tags.Select(tag => tag.Name).OrderBy(tag => tag).ToList(),
            item.GameMedia,
            item.GamePlatform,
            item.FinishedOnAnotherPlatform,
            item.ChecklistEntries.Count,
            item.ChecklistEntries.Count(entry => entry.IsCompleted));
    }

    private static string GetUnitTitle(string category, EpisodeEditorModel entry, int index)
        => string.IsNullOrWhiteSpace(entry.Title) ? ArchiveMetadata.GetUnitTitle(category, index + 1) : entry.Title.Trim();

    private static void ReplaceTags(ArchiveItem entity, string tagsText)
    {
        entity.Tags.Clear();

        foreach (var tag in ParseTagNames(tagsText))
        {
            entity.Tags.Add(new ItemTag { Name = tag });
        }
    }

    private static IEnumerable<string> ParseTagNames(string tagsText)
        => tagsText.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static void ReplaceChecklist(ArchiveItem entity, IEnumerable<ChecklistRecord> checklistEntries)
    {
        entity.ChecklistEntries.Clear();

        var index = 0;
        foreach (var entry in checklistEntries.Where(current => !string.IsNullOrWhiteSpace(current.Title)))
        {
            entity.ChecklistEntries.Add(new ItemChecklistEntry
            {
                ArchiveItemId = entity.Id,
                Title = entry.Title.Trim(),
                IsCompleted = entry.IsCompleted,
                SortOrder = index++
            });
        }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<List<ArchiveItemRecord>> LoadExportRecordsAsync(MyArchiveDbContext db)
    {
        return await db.Items
            .AsNoTracking()
            .Include(item => item.Tags)
            .Include(item => item.ChecklistEntries)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Title)
            .Select(item => new ArchiveItemRecord(
                item.Id,
                item.Title,
                item.Type,
                item.CatalogStatus,
                item.CoverImageUrl,
                item.Rating,
                item.Review,
                item.Notes,
                item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList(),
                item.GameMedia,
                item.GamePlatform,
                item.FinishedOnAnotherPlatform,
                item.CreatedAt,
                item.CompletedAt,
                item.ChecklistEntries.OrderBy(entry => entry.SortOrder).Select(entry => new ChecklistRecord(entry.Title, entry.IsCompleted)).ToList()))
            .ToListAsync();
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string ToSingleLine(string value) => value.Replace("\r\n", " / ").Replace('\n', ' ').Replace('\r', ' ');

    private static List<string[]> ParseCsv(string content)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inQuotes)
            {
                if (character == '"' && index + 1 < content.Length && content[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(current.ToString());
                    current.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(current.ToString());
                    rows.Add(row.ToArray());
                    row = [];
                    current.Clear();
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        if (current.Length > 0 || row.Count > 0)
        {
            row.Add(current.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string GetValue(string[] row, IReadOnlyDictionary<string, int> index, string column)
        => index.TryGetValue(column, out var valueIndex) && valueIndex < row.Length ? row[valueIndex] : string.Empty;

    private static DateTime? ParseDate(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) ? result : null;

    private static Guid ParseGuid(string value) => Guid.TryParse(value, out var result) ? result : Guid.Empty;

    private static double? ParseNullableDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static bool ParseBool(string value)
        => bool.TryParse(value, out var result) && result;

    private static IReadOnlyList<string> SplitPipeList(string value)
        => value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<ChecklistRecord> SplitChecklist(string value)
        => value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry =>
            {
                var parts = entry.Split("::", 2, StringSplitOptions.TrimEntries);
                var title = parts.ElementAtOrDefault(0) ?? string.Empty;
                var done = bool.TryParse(parts.ElementAtOrDefault(1), out var parsed) && parsed;
                return new ChecklistRecord(title, done);
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
            .ToList();

    private sealed record ArchiveExportDocument(DateTime ExportedAt, List<ArchiveItemRecord> Items);

    private sealed record ArchiveItemRecord(
        Guid Id,
        string Title,
        string Category,
        string Status,
        string CoverImageUrl,
        double? Rating,
        string? Review,
        string? Notes,
        IReadOnlyList<string> Tags,
        string? GameMedia,
        string? GamePlatform,
        bool FinishedOnAnotherPlatform,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        IReadOnlyList<ChecklistRecord> Checklist);

    private sealed record ChecklistRecord(string Title, bool IsCompleted);
}
