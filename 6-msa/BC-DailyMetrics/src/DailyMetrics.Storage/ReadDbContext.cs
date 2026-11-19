namespace DailyMetrics.Storage;
using Microsoft.EntityFrameworkCore;
using DailyMetrics.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<DailyMetricsReadModel> Items => Set<DailyMetricsReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<DailyMetricsReadModel>(e => { e.ToTable("daily_metrics_read"); e.HasKey(x => x.Id); e.HasIndex(x => x.ClientId); }); }
}
