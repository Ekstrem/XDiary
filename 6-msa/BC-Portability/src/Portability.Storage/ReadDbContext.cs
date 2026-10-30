namespace Portability.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<CsvDocumentEntity> Documents => Set<CsvDocumentEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<CsvDocumentEntity>(e => { e.ToTable("csv_documents"); e.HasKey(x => x.Id); }); }
}