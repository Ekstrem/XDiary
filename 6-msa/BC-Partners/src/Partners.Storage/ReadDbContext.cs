namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<PartnerEntity> Partners => Set<PartnerEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<PartnerEntity>(e => { e.ToTable("partners"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(256).IsRequired(); }); }
}