using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using MyArchive.Models;
using MyArchive.Shared;
using Npgsql;

namespace MyArchive.Data;

public static class ArchiveDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("MyArchive")
                               ?? "Host=localhost;Port=5432;Database=myarchive;Username=postgres;Password=postgres";

        try
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyArchiveDbContext>>();
            await using var db = await factory.CreateDbContextAsync();

            await EnsureApplicationSchemaAsync(db);
            await EnsureExtendedSchemaAsync(db);
            await BackfillCatalogFieldsAsync(db);

            if (await db.Items.AnyAsync())
            {
                await db.SaveChangesAsync();
                return;
            }

            db.Items.AddRange(CreateSeedItems());
            await db.SaveChangesAsync();
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            throw new InvalidOperationException(
                $"""
                Nao foi possivel conectar ao PostgreSQL durante a inicializacao do MyArchive.

                Connection string atual:
                {connectionString}

                Verifique se o banco esta acessivel e se a connection string esta correta.
                Para desenvolvimento local com Docker, rode na raiz do repositorio:
                docker compose up -d postgres
                """,
                exception);
        }
    }

    private static bool IsConnectionFailure(Exception exception)
        => exception is NpgsqlException and not PostgresException
           || exception is SocketException
           || exception.InnerException is not null && IsConnectionFailure(exception.InnerException);

    private static async Task EnsureApplicationSchemaAsync(MyArchiveDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Items" (
                "Id" uuid NOT NULL,
                "Title" character varying(160) NOT NULL,
                "Description" character varying(4000) NOT NULL DEFAULT '',
                "Type" character varying(80) NOT NULL,
                "Status" integer NOT NULL DEFAULT 1,
                "Priority" integer NOT NULL DEFAULT 2,
                "CatalogStatus" character varying(80) NOT NULL DEFAULT '',
                "CoverImageUrl" character varying(500) NOT NULL DEFAULT '',
                "Rating" double precision NULL,
                "Review" character varying(20000) NULL,
                "GameMedia" character varying(40) NULL,
                "GamePlatform" character varying(60) NULL,
                "FinishedOnAnotherPlatform" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "CompletedAt" timestamp with time zone NULL,
                "ProgressPercent" double precision NOT NULL DEFAULT 0,
                "ProgressLabel" character varying(120) NULL,
                "Notes" character varying(12000) NULL,
                CONSTRAINT "PK_Items" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "ItemTags" (
                "Id" uuid NOT NULL,
                "ArchiveItemId" uuid NOT NULL,
                "Name" character varying(40) NOT NULL,
                CONSTRAINT "PK_ItemTags" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ItemTags_Items_ArchiveItemId" FOREIGN KEY ("ArchiveItemId") REFERENCES "Items" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS "ChecklistEntries" (
                "Id" uuid NOT NULL,
                "ArchiveItemId" uuid NOT NULL,
                "Title" character varying(200) NOT NULL,
                "IsCompleted" boolean NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_ChecklistEntries" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ChecklistEntries_Items_ArchiveItemId" FOREIGN KEY ("ArchiveItemId") REFERENCES "Items" ("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_Items_Type" ON "Items" ("Type");
            CREATE INDEX IF NOT EXISTS "IX_Items_Status" ON "Items" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_Items_Priority" ON "Items" ("Priority");
            CREATE INDEX IF NOT EXISTS "IX_Items_CatalogStatus" ON "Items" ("CatalogStatus");
            CREATE INDEX IF NOT EXISTS "IX_ItemTags_ArchiveItemId" ON "ItemTags" ("ArchiveItemId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ItemTags_ArchiveItemId_Name" ON "ItemTags" ("ArchiveItemId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_ChecklistEntries_ArchiveItemId" ON "ChecklistEntries" ("ArchiveItemId");
            """);
    }

    private static async Task EnsureExtendedSchemaAsync(MyArchiveDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "CatalogStatus" character varying(80) NOT NULL DEFAULT '';
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "CoverImageUrl" character varying(500) NOT NULL DEFAULT '';
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "Rating" double precision NULL;
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "Review" character varying(20000) NULL;
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "GameMedia" character varying(40) NULL;
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "GamePlatform" character varying(60) NULL;
            ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "FinishedOnAnotherPlatform" boolean NOT NULL DEFAULT FALSE;
            CREATE INDEX IF NOT EXISTS "IX_Items_CatalogStatus" ON "Items" ("CatalogStatus");
            """);
    }

    private static async Task BackfillCatalogFieldsAsync(MyArchiveDbContext db)
    {
        var items = await db.Items
            .Include(item => item.Tags)
            .ToListAsync();

        foreach (var item in items)
        {
            item.Type = ArchiveMetadata.NormalizeCategory(item.Type);

            if (string.IsNullOrWhiteSpace(item.CatalogStatus))
            {
                item.CatalogStatus = MapLegacyStatus(item.Type, item.Status);
            }
            else
            {
                item.CatalogStatus = ArchiveMetadata.NormalizeStatus(item.Type, item.CatalogStatus);
            }

            if (string.IsNullOrWhiteSpace(item.CoverImageUrl))
            {
                item.CoverImageUrl = ArchiveMetadata.DefaultCoverPath;
            }

            if (ArchiveMetadata.IsCompleted(item.Type, item.CatalogStatus) && !item.CompletedAt.HasValue)
            {
                item.CompletedAt = item.UpdatedAt;
            }
        }
    }

    private static string MapLegacyStatus(string category, ItemStatus status)
        => status switch
        {
            ItemStatus.InProgress => ArchiveMetadata.GetStatuses(category)[1],
            ItemStatus.Completed => ArchiveMetadata.GetStatuses(category).FirstOrDefault(current =>
                ArchiveMetadata.IsCompleted(category, current)) ?? ArchiveMetadata.GetStatuses(category).Last(),
            _ => ArchiveMetadata.GetStatuses(category).First()
        };

    private static IEnumerable<ArchiveItem> CreateSeedItems()
    {
        return
        [
            new ArchiveItem
            {
                Title = "O Nome do Vento",
                Type = "Livros",
                CatalogStatus = "Lendo",
                CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
                Rating = 9.2,
                Review = "Fantasia com foco forte em atmosfera, narrativa oral e construcao do protagonista.",
                Notes = "Retomar a leitura do arco em Imre.",
                CreatedAt = DateTime.UtcNow.AddDays(-18),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new ArchiveItem
            {
                Title = "Berserk Deluxe Vol. 1",
                Type = "Mangas",
                CatalogStatus = "Nao lido",
                CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
                Notes = "Prioridade alta para abrir a colecao fisica.",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ArchiveItem
            {
                Title = "The Witcher 3",
                Type = "Games",
                CatalogStatus = "Jogando",
                CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
                Rating = 9.7,
                Review = "Segue como um dos melhores RPGs de mundo aberto do acervo.",
                Notes = "Finalizar Hearts of Stone antes de Blood and Wine.",
                GameMedia = "Digital",
                GamePlatform = "PC",
                FinishedOnAnotherPlatform = true,
                CreatedAt = DateTime.UtcNow.AddDays(-28),
                UpdatedAt = DateTime.UtcNow.AddHours(-12)
            },
            new ArchiveItem
            {
                Title = "Duna: Parte Dois",
                Type = "Filmes",
                CatalogStatus = "Assistido",
                CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
                Rating = 8.8,
                Review = "Escala visual absurda e direcao muito segura.",
                CompletedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ArchiveItem
            {
                Title = "Frieren",
                Type = "Animes",
                CatalogStatus = "Vendo",
                CoverImageUrl = ArchiveMetadata.DefaultCoverPath,
                Rating = 9.1,
                Notes = "Registrar impressoes por arco quando o suporte a episodios entrar.",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        ];
    }
}
