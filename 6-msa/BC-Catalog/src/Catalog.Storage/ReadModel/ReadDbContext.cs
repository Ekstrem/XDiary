using Catalog.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) каталога.
/// Хранит проекцию `catalog` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogReadModel> CatalogItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogReadModel>(entity =>
        {
            entity.ToTable("catalog");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Kind).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Photo).HasColumnType("text");
            entity.Property(e => e.Notes).HasColumnType("text");
            entity.Property(e => e.Brand).HasMaxLength(256);
            entity.Property(e => e.Partner).HasMaxLength(256);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Kind);
            entity.HasIndex(e => e.Name);
        });
    }
}
