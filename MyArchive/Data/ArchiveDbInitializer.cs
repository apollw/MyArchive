using Microsoft.EntityFrameworkCore;
using MyArchive.Models;
using MyArchive.Shared;

namespace MyArchive.Data;

public static class ArchiveDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyArchiveDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        await db.Database.EnsureCreatedAsync();

        if (await db.Items.AnyAsync())
        {
            return;
        }

        var items = new[]
        {
            new ArchiveItem
            {
                Title = "Clean Architecture",
                Description = "Leitura de referencia para refinar a estrutura do projeto.",
                Type = "Livro",
                Status = ItemStatus.InProgress,
                Priority = ItemPriority.High,
                ProgressPercent = 42,
                ProgressLabel = "Capitulo 6 de 14",
                Notes = "Anotar decisoes de arquitetura aplicaveis ao acervo.",
                Tags =
                {
                    new ItemTag { Name = ".NET" },
                    new ItemTag { Name = "Arquitetura" }
                },
                ChecklistEntries =
                {
                    new ItemChecklistEntry { Title = "Ler capitulo sobre casos de uso", IsCompleted = true, SortOrder = 0 },
                    new ItemChecklistEntry { Title = "Revisar capitulo sobre persistencia", IsCompleted = false, SortOrder = 1 }
                }
            },
            new ArchiveItem
            {
                Title = "The Witcher 3",
                Description = "Continuar campanha principal e expansoes.",
                Type = "Jogo",
                Status = ItemStatus.Paused,
                Priority = ItemPriority.Medium,
                ProgressPercent = 63,
                ProgressLabel = "Skellige em andamento",
                Tags =
                {
                    new ItemTag { Name = "RPG" },
                    new ItemTag { Name = "Backlog" }
                }
            },
            new ArchiveItem
            {
                Title = "ASP.NET Core Deep Dive",
                Description = "Curso para revisar Blazor, APIs e observabilidade.",
                Type = "Curso",
                Status = ItemStatus.NotStarted,
                Priority = ItemPriority.Critical,
                ProgressPercent = 0,
                ProgressLabel = "0 de 12 modulos",
                Tags =
                {
                    new ItemTag { Name = ".NET" },
                    new ItemTag { Name = "Estudo" }
                }
            },
            new ArchiveItem
            {
                Title = "Duna: Parte Dois",
                Description = "Registrar observacoes apos assistir.",
                Type = "Filme",
                Status = ItemStatus.Completed,
                Priority = ItemPriority.Low,
                ProgressPercent = 100,
                ProgressLabel = "Assistido",
                CompletedAt = DateTime.UtcNow.AddDays(-2),
                Tags =
                {
                    new ItemTag { Name = "Sci-Fi" }
                }
            }
        };

        db.Items.AddRange(items);
        await db.SaveChangesAsync();
    }
}
