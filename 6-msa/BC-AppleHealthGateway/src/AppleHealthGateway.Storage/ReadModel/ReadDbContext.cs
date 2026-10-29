using AppleHealthGateway.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace AppleHealthGateway.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) шлюза Apple Health.
/// Хранит проекцию `apple_import` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<AppleImportReadModel> ImportStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppleImportReadModel>(entity =>
        {
            entity.ToTable("apple_import");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Metrics).HasColumnType("text[]");
            entity.Property(e => e.LastImport).HasMaxLength(64);
            entity.Property(e => e.ProcessedFiles).HasColumnType("text[]");

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.SourceId).IsUnique();
        });
    }
}
