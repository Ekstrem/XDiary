namespace Portability.Storage;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Portability.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<CsvDocumentReadModel> Items => Set<CsvDocumentReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CsvDocumentReadModel>(e =>
        {
            e.ToTable("csv_document_read");
            e.HasKey(x => x.Id);
            e.Property(x => x.Headers).HasConversion(StringList());
            e.Property(x => x.Rows).HasConversion(StringList());
        });
    }
    private static ValueConverter<IReadOnlyList<string>, string> StringList()
        => new(v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions)null!),
               s => JsonSerializer.Deserialize<List<string>>(s ?? "[]", (JsonSerializerOptions)null!) ?? new List<string>());
}
