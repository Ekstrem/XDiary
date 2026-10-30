using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Journal.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) журнала.
/// Хранит проекцию `records` (схема public — профиль Single), индекс по startedAt.
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<JournalReadModel> Records { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JournalReadModel>(entity =>
        {
            entity.ToTable("records");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PartnerId).HasMaxLength(64);
            entity.Property(e => e.StartedAt).HasMaxLength(32);
            entity.Property(e => e.Place).HasMaxLength(256);
            entity.Property(e => e.Positions).HasColumnType("text[]");
            entity.Property(e => e.Tags).HasColumnType("text[]");
            entity.Property(e => e.Costumes).HasColumnType("text[]");
            entity.Property(e => e.Toys).HasColumnType("text[]");
            entity.Property(e => e.Fantasies).HasColumnType("text[]");
            entity.Property(e => e.Contraceptives).HasColumnType("text[]");
            entity.Property(e => e.OrgasmMeType).HasColumnType("text[]");
            entity.Property(e => e.OrgasmPartnerType).HasColumnType("text[]");
            entity.Property(e => e.GarminId).HasMaxLength(128);

            entity.HasIndex(e => e.StartedAt);
        });
    }
}
