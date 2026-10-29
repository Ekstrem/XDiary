using GarminGateway.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace GarminGateway.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) шлюза Garmin.
/// Хранит проекцию `garmin_sync` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<GarminSyncReadModel> SyncStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GarminSyncReadModel>(entity =>
        {
            entity.ToTable("garmin_sync");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ApiKey).HasMaxLength(512);
            entity.Property(e => e.OauthToken).HasMaxLength(512);
            entity.Property(e => e.TokenExpiry).HasMaxLength(64);
            entity.Property(e => e.Metrics).HasColumnType("text[]");
            entity.Property(e => e.LastSync).HasMaxLength(64);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.SourceId).IsUnique();
        });
    }
}
