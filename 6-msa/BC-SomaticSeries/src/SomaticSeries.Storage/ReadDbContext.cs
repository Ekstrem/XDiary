namespace SomaticSeries.Storage;
using Microsoft.EntityFrameworkCore;
using SomaticSeries.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<SomaticSeriesReadModel> Items => Set<SomaticSeriesReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<SomaticSeriesReadModel>(e => { e.ToTable("somatic_read"); e.HasKey(x => x.Id); e.HasIndex(x => x.ClientId); }); }
}
