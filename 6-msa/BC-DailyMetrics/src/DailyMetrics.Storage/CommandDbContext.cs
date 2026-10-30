namespace DailyMetrics.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class CommandDbContext : DbContext
{
    public CommandDbContext(DbContextOptions<CommandDbContext> options) : base(options) { }
    public DbSet<DailyMetricsEntity> Daily => Set<DailyMetricsEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<DailyMetricsEntity>(e => { e.ToTable("daily_metrics"); e.HasKey(x => x.Id); e.HasIndex(x => x.Date).IsUnique(); e.Property(x => x.Date).HasMaxLength(10).IsRequired(); }); }
}