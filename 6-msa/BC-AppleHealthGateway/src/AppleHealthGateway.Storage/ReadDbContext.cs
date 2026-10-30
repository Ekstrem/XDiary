namespace AppleHealthGateway.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<AppleImportEntity> Import => Set<AppleImportEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<AppleImportEntity>(e => { e.ToTable("apple_import"); e.HasKey(x => x.Id); e.HasIndex(x => x.SourceId).IsUnique(); e.Property(x => x.SourceId).HasMaxLength(32).IsRequired(); }); }
}