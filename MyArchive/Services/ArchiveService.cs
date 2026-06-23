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
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Title)
            .ToListAsync();

        var summaries = items.Select(MapSummary).ToList();

        var metrics = new List<DashboardMetric>
        {
            new("Total de itens", summaries.Count.ToString(CultureInfo.InvariantCulture), "accent-slate"),
            new("Em andamento", summaries.Count(item => item.Status == ItemStatus.InProgress).ToString(CultureInfo.InvariantCulture), "accent-gold"),
            new("Concluidos", summaries.Count(item => item.Status == ItemStatus.Completed).ToString(CultureInfo.InvariantCulture), "accent-green"),
            new("Prioridade critica", summaries.Count(item => item.Priority == ItemPriority.Critical).ToString(CultureInfo.InvariantCulture), "accent-red")
        };

        var statusBreakdown = Enum.GetValues<ItemStatus>()
            .Select(status => new DashboardTimelinePoint(status.GetLabel(), summaries.Count(item => item.Status == status)))
            .ToList();

        var typeBreakdown = summaries
            .GroupBy(item => item.Type)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(6)
            .Select(group => new DashboardTimelinePoint(group.Key, group.Count()))
            .ToList();

        return new DashboardSummary
        {
            Metrics = metrics,
            InProgress = summaries.Where(item => item.Status == ItemStatus.InProgress)
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.ProgressPercent)
                .Take(5)
                .ToList(),
            RecentlyCompleted = summaries.Where(item => item.CompletedAt.HasValue)
                .OrderByDescending(item => item.CompletedAt)
                .Take(5)
                .ToList(),
            PriorityFocus = summaries
                .Where(item => item.Status != ItemStatus.Completed && item.Status != ItemStatus.Abandoned)
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.CreatedAt)
                .Take(5)
                .ToList(),
            StatusBreakdown = statusBreakdown,
            TypeBreakdown = typeBreakdown
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
            .ThenByDescending(item => item.Priority)
            .ToListAsync();

        var summaries = items.Select(MapSummary).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            summaries = summaries.Where(item =>
                    item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            summaries = summaries.Where(item => string.Equals(item.Type, query.Type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            summaries = summaries.Where(item => item.Tags.Any(tag => string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        if (query.Status.HasValue)
        {
            summaries = summaries.Where(item => item.Status == query.Status.Value).ToList();
        }

        var groups = BuildGroups(summaries, query.GroupBy);

        return new ArchiveCatalog
        {
            Items = summaries,
            Groups = groups,
            AvailableTypes = items.Select(item => item.Type).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            AvailableTags = items.SelectMany(item => item.Tags.Select(tag => tag.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList()
        };
    }

    public async Task<ItemEditorModel> CreateEditorModelAsync()
    {
        await Task.CompletedTask;
        return new ItemEditorModel
        {
            Type = ArchiveMetadata.SuggestedTypes.First(),
            CreatedAt = DateTime.Now,
            ChecklistEntries = []
        };
    }

    public async Task<ItemEditorModel?> GetItemAsync(Guid id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.Items
            .AsNoTracking()
            .Include(current => current.Tags)
            .Include(current => current.ChecklistEntries.OrderBy(entry => entry.SortOrder))
            .FirstOrDefaultAsync(current => current.Id == id);

        return item is null ? null : new ItemEditorModel
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Type = item.Type,
            Status = item.Status,
            Priority = item.Priority,
            ProgressPercent = item.ProgressPercent,
            ProgressLabel = item.ProgressLabel ?? string.Empty,
            Notes = item.Notes ?? string.Empty,
            TagsText = string.Join(", ", item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name)),
            CreatedAt = item.CreatedAt.ToLocalTime(),
            CompletedAt = item.CompletedAt?.ToLocalTime(),
            ChecklistEntries = item.ChecklistEntries
                .OrderBy(entry => entry.SortOrder)
                .Select(entry => new ChecklistEntryModel
                {
                    Id = entry.Id,
                    Title = entry.Title,
                    IsCompleted = entry.IsCompleted
                })
                .ToList()
        };
    }

    public async Task<Guid> SaveItemAsync(ItemEditorModel model)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var entity = model.Id.HasValue
            ? await db.Items.Include(item => item.Tags).Include(item => item.ChecklistEntries).FirstAsync(item => item.Id == model.Id.Value)
            : new ArchiveItem();

        entity.Title = model.Title.Trim();
        entity.Description = model.Description.Trim();
        entity.Type = model.Type.Trim();
        entity.Status = model.Status;
        entity.Priority = model.Priority;
        entity.ProgressPercent = Math.Clamp(model.ProgressPercent, 0, 100);
        entity.ProgressLabel = NullIfWhiteSpace(model.ProgressLabel);
        entity.Notes = NullIfWhiteSpace(model.Notes);
        entity.CreatedAt = model.Id.HasValue ? entity.CreatedAt : DateTime.SpecifyKind(model.CreatedAt, DateTimeKind.Local).ToUniversalTime();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.CompletedAt = model.Status == ItemStatus.Completed
            ? model.CompletedAt?.ToUniversalTime() ?? DateTime.UtcNow
            : null;

        ReplaceTags(entity, model.TagsText);
        ReplaceChecklist(entity, model.ChecklistEntries);

        if (!model.Id.HasValue)
        {
            db.Items.Add(entity);
        }

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
        builder.AppendLine("Id,Title,Description,Type,Status,Priority,CreatedAt,CompletedAt,ProgressPercent,ProgressLabel,Notes,Tags,Checklist");

        foreach (var item in items.OrderBy(current => current.Type).ThenBy(current => current.Title))
        {
            builder.AppendJoin(',',
                EscapeCsv(item.Id.ToString()),
                EscapeCsv(item.Title),
                EscapeCsv(ToSingleLine(item.Description)),
                EscapeCsv(item.Type),
                EscapeCsv(item.Status),
                EscapeCsv(item.Priority),
                EscapeCsv(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                EscapeCsv(item.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(item.ProgressPercent.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(item.ProgressLabel ?? string.Empty),
                EscapeCsv(ToSingleLine(item.Notes ?? string.Empty)),
                EscapeCsv(string.Join('|', item.Tags)),
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

        foreach (var item in items.OrderBy(current => current.Type).ThenBy(current => current.Title))
        {
            builder.AppendLine($"## {item.Title}");
            builder.AppendLine();
            builder.AppendLine($"- Tipo: {item.Type}");
            builder.AppendLine($"- Status: {item.Status}");
            builder.AppendLine($"- Prioridade: {item.Priority}");
            builder.AppendLine($"- Progresso: {item.ProgressPercent:0}% {(string.IsNullOrWhiteSpace(item.ProgressLabel) ? string.Empty : $"({item.ProgressLabel})")}".TrimEnd());
            builder.AppendLine($"- Criado em: {item.CreatedAt:dd/MM/yyyy}");

            if (item.CompletedAt.HasValue)
            {
                builder.AppendLine($"- Concluido em: {item.CompletedAt:dd/MM/yyyy}");
            }

            if (item.Tags.Count > 0)
            {
                builder.AppendLine($"- Tags: {string.Join(", ", item.Tags)}");
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                builder.AppendLine();
                builder.AppendLine(item.Description);
            }

            if (item.Checklist.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("### Checklist");
                builder.AppendLine();

                foreach (var entry in item.Checklist)
                {
                    builder.AppendLine($"- [{(entry.IsCompleted ? "x" : " ")}] {entry.Title}");
                }
            }

            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                builder.AppendLine();
                builder.AppendLine("### Notas");
                builder.AppendLine();
                builder.AppendLine(item.Notes);
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
            created.Add(new ArchiveItemRecord(
                ParseGuid(GetValue(row, index, "Id")),
                GetValue(row, index, "Title"),
                GetValue(row, index, "Description"),
                GetValue(row, index, "Type"),
                GetValue(row, index, "Status"),
                GetValue(row, index, "Priority"),
                ParseDate(GetValue(row, index, "CreatedAt")) ?? DateTime.UtcNow,
                ParseDate(GetValue(row, index, "CompletedAt")),
                ParseDouble(GetValue(row, index, "ProgressPercent")),
                GetValue(row, index, "ProgressLabel"),
                GetValue(row, index, "Notes"),
                SplitPipeList(GetValue(row, index, "Tags")),
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
                entity = new ArchiveItem
                {
                    Id = id
                };
                db.Items.Add(entity);
                created++;
            }
            else
            {
                updated++;
            }

            entity.Title = record.Title.Trim();
            entity.Description = record.Description.Trim();
            entity.Type = string.IsNullOrWhiteSpace(record.Type) ? "Outro" : record.Type.Trim();
            entity.Status = ParseStatus(record.Status);
            entity.Priority = ParsePriority(record.Priority);
            entity.CreatedAt = record.CreatedAt == default ? DateTime.UtcNow : DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.CompletedAt = record.CompletedAt;
            entity.ProgressPercent = Math.Clamp(record.ProgressPercent, 0, 100);
            entity.ProgressLabel = NullIfWhiteSpace(record.ProgressLabel);
            entity.Notes = NullIfWhiteSpace(record.Notes);

            ReplaceTags(entity, string.Join(", ", record.Tags));
            ReplaceChecklist(entity, record.Checklist.Select(entry => new ChecklistEntryModel
            {
                Title = entry.Title,
                IsCompleted = entry.IsCompleted
            }));
        }

        await db.SaveChangesAsync();
        return new ImportSummary(created, updated, created + updated, "Importacao concluida com sucesso.");
    }

    private static IReadOnlyList<ArchiveGroup> BuildGroups(IEnumerable<ArchiveListItemSummary> items, ArchiveGroupBy groupBy)
    {
        return groupBy switch
        {
            ArchiveGroupBy.None => [new ArchiveGroup("Todos os itens", items.OrderByDescending(item => item.Priority).ThenBy(item => item.Title).ToList())],
            ArchiveGroupBy.Type => items
                .GroupBy(item => item.Type)
                .OrderBy(group => group.Key)
                .Select(group => new ArchiveGroup(group.Key, group.OrderByDescending(item => item.Priority).ThenBy(item => item.Title).ToList()))
                .ToList(),
            ArchiveGroupBy.Status => items
                .GroupBy(item => item.Status)
                .OrderBy(group => group.Key)
                .Select(group => new ArchiveGroup(group.Key.GetLabel(), group.OrderByDescending(item => item.Priority).ThenBy(item => item.Title).ToList()))
                .ToList(),
            ArchiveGroupBy.Tag => items
                .SelectMany(item => item.Tags.Count > 0
                    ? item.Tags.Select(tag => new { Group = tag, Item = item })
                    : [new { Group = "Sem tags", Item = item }])
                .GroupBy(entry => entry.Group)
                .OrderBy(group => group.Key)
                .Select(group => new ArchiveGroup(group.Key, group.Select(entry => entry.Item).OrderByDescending(item => item.Priority).ThenBy(item => item.Title).ToList()))
                .ToList(),
            _ => []
        };
    }

    private static ArchiveListItemSummary MapSummary(ArchiveItem item)
    {
        var tags = item.Tags
            .Select(tag => tag.Name)
            .OrderBy(tag => tag)
            .ToList();

        var checklistTotal = item.ChecklistEntries.Count;
        var checklistCompleted = item.ChecklistEntries.Count(entry => entry.IsCompleted);

        return new ArchiveListItemSummary(
            item.Id,
            item.Title,
            item.Description,
            item.Type,
            item.Status,
            item.Priority,
            item.ProgressPercent,
            item.ProgressLabel ?? string.Empty,
            item.CreatedAt.ToLocalTime(),
            item.CompletedAt?.ToLocalTime(),
            item.Notes ?? string.Empty,
            tags,
            checklistCompleted,
            checklistTotal);
    }

    private static void ReplaceTags(ArchiveItem entity, string tagsText)
    {
        entity.Tags.Clear();

        foreach (var tag in tagsText.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            entity.Tags.Add(new ItemTag { Name = tag });
        }
    }

    private static void ReplaceChecklist(ArchiveItem entity, IEnumerable<ChecklistEntryModel> checklistEntries)
    {
        entity.ChecklistEntries.Clear();

        var index = 0;
        foreach (var entry in checklistEntries.Where(current => !string.IsNullOrWhiteSpace(current.Title)))
        {
            entity.ChecklistEntries.Add(new ItemChecklistEntry
            {
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
                item.Description,
                item.Type,
                item.Status.ToString(),
                item.Priority.ToString(),
                item.CreatedAt,
                item.CompletedAt,
                item.ProgressPercent,
                item.ProgressLabel,
                item.Notes,
                item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList(),
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

    private static double ParseDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static DateTime? ParseDate(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) ? result : null;

    private static Guid ParseGuid(string value) => Guid.TryParse(value, out var result) ? result : Guid.Empty;

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

    private static ItemStatus ParseStatus(string value)
        => Enum.TryParse<ItemStatus>(value, true, out var parsed) ? parsed : ItemStatus.NotStarted;

    private static ItemPriority ParsePriority(string value)
        => Enum.TryParse<ItemPriority>(value, true, out var parsed) ? parsed : ItemPriority.Medium;

    private sealed record ArchiveExportDocument(DateTime ExportedAt, List<ArchiveItemRecord> Items);

    private sealed record ArchiveItemRecord(
        Guid Id,
        string Title,
        string Description,
        string Type,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        double ProgressPercent,
        string? ProgressLabel,
        string? Notes,
        IReadOnlyList<string> Tags,
        IReadOnlyList<ChecklistRecord> Checklist);

    private sealed record ChecklistRecord(string Title, bool IsCompleted);
}
