namespace Catalog.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class CommandDbContext : DbContext
{
    public CommandDbContext(DbContextOptions<CommandDbContext> options) : base(options) { }
    public DbSet<CatalogItemEntity> Items => Set<CatalogItemEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<CatalogItemEntity>(e => { e.ToTable("catalog_items"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(256).IsRequired(); e.Property(x => x.Kind).HasMaxLength(32).IsRequired(); }); }
}