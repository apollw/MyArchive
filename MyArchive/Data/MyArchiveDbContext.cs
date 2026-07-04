using Microsoft.EntityFrameworkCore;
using MyArchive.Models;

namespace MyArchive.Data;

public sealed class MyArchiveDbContext(DbContextOptions<MyArchiveDbContext> options) : DbContext(options)
{
    public DbSet<ArchiveItem> Items => Set<ArchiveItem>();
    public DbSet<ItemTag> Tags => Set<ItemTag>();
    public DbSet<ItemChecklistEntry> ChecklistEntries => Set<ItemChecklistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArchiveItem>(entity =>
        {
            entity.ToTable("Items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(4000);
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CatalogStatus).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CoverImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Review).HasMaxLength(20000);
            entity.Property(item => item.GameMedia).HasMaxLength(40);
            entity.Property(item => item.GamePlatform).HasMaxLength(60);
            entity.Property(item => item.ProgressLabel).HasMaxLength(120);
            entity.Property(item => item.Notes).HasMaxLength(12000);
            entity.HasIndex(item => item.Type);
            entity.HasIndex(item => item.CatalogStatus);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.Priority);
            entity.HasMany(item => item.Tags)
                .WithOne()
                .HasForeignKey(tag => tag.ArchiveItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.ChecklistEntries)
                .WithOne()
                .HasForeignKey(entry => entry.ArchiveItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemTag>(entity =>
        {
            entity.ToTable("ItemTags");
            entity.HasKey(tag => tag.Id);
            entity.Property(tag => tag.Name).HasMaxLength(40).IsRequired();
            entity.HasIndex(tag => new { tag.ArchiveItemId, tag.Name }).IsUnique();
        });

        modelBuilder.Entity<ItemChecklistEntry>(entity =>
        {
            entity.ToTable("ChecklistEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        });
    }
}
