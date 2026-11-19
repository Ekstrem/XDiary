namespace Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Catalog.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<CatalogReadModel> Items => Set<CatalogReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<CatalogReadModel>(e => { e.ToTable("catalog_read"); e.HasKey(x => x.Id); e.HasIndex(x => x.ClientId); }); }
}
