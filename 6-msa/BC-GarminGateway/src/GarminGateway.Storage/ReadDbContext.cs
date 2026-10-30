namespace GarminGateway.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<GarminSyncEntity> Sync => Set<GarminSyncEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<GarminSyncEntity>(e => { e.ToTable("garmin_sync"); e.HasKey(x => x.Id); e.HasIndex(x => x.SourceId).IsUnique(); e.Property(x => x.SourceId).HasMaxLength(32).IsRequired(); }); }
}