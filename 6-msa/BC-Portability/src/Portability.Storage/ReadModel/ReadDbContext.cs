using Portability.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Portability.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) переносимости.
/// Хранит проекцию `csv_documents` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<CsvDocumentReadModel> CsvDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CsvDocumentReadModel>(entity =>
        {
            entity.ToTable("csv_documents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Headers).HasColumnType("text[]");
            entity.Property(e => e.Rows).HasColumnType("text[]");

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.SourceId).IsUnique();
        });
    }
}
