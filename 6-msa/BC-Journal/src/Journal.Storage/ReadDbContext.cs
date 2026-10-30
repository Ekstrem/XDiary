namespace Journal.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<RecordEntity> Records => Set<RecordEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RecordEntity>(e => { e.ToTable("records"); e.HasKey(x => x.Id); e.HasIndex(x => x.StartedAt); });
    }
}