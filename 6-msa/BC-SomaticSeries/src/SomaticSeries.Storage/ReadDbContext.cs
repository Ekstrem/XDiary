namespace SomaticSeries.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<SomaticPointEntity> Points => Set<SomaticPointEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<SomaticPointEntity>(e => { e.ToTable("somatic_points"); e.HasKey(x => x.Id); e.HasIndex(x => x.T); e.Property(x => x.T).HasMaxLength(16).IsRequired(); }); }
}