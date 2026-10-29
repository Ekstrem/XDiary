using DailyMetrics.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace DailyMetrics.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) дневных метрик.
/// Хранит проекцию `daily_metrics` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<DailyMetricsReadModel> DailyMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyMetricsReadModel>(entity =>
        {
            entity.ToTable("daily_metrics");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Date).HasMaxLength(16).IsRequired();

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Date).IsUnique();
        });
    }
}
