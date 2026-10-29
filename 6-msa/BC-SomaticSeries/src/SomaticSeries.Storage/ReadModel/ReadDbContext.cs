using SomaticSeries.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace SomaticSeries.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) соматики.
/// Хранит проекцию `somatic` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<SomaticSeriesReadModel> SomaticPoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SomaticSeriesReadModel>(entity =>
        {
            entity.ToTable("somatic");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.T).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Hr).IsRequired();
            entity.Property(e => e.BodyBattery).IsRequired();

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.T).IsUnique();
        });
    }
}
