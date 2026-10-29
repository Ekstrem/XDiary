using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Journal.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) журнала.
/// Хранит проекцию `records` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<JournalReadModel> JournalRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JournalReadModel>(entity =>
        {
            entity.ToTable("records");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StartedAt).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Partner).HasMaxLength(256);
            entity.Property(e => e.Place).HasMaxLength(256);
            entity.Property(e => e.GarminId).HasMaxLength(256);

            entity.Property(e => e.Positions).HasColumnType("text[]");
            entity.Property(e => e.Tags).HasColumnType("text[]");
            entity.Property(e => e.Costumes).HasColumnType("text[]");
            entity.Property(e => e.Toys).HasColumnType("text[]");
            entity.Property(e => e.Fantasies).HasColumnType("text[]");
            entity.Property(e => e.Contraceptives).HasColumnType("text[]");
            entity.Property(e => e.OrgasmMeType).HasColumnType("text[]");
            entity.Property(e => e.OrgasmPartnerType).HasColumnType("text[]");

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.StartedAt);
        });
    }
}
