namespace AppleHealthGateway.Storage;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using AppleHealthGateway.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<AppleImportReadModel> Items => Set<AppleImportReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppleImportReadModel>(e =>
        {
            e.ToTable("apple_health_read");
            e.HasKey(x => x.Id);
            e.Property(x => x.Metrics).HasConversion(StringList());
            e.Property(x => x.ProcessedFiles).HasConversion(StringList());
        });
    }
    private static ValueConverter<IReadOnlyList<string>, string> StringList()
        => new(v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions)null!),
               s => JsonSerializer.Deserialize<List<string>>(s ?? "[]", (JsonSerializerOptions)null!) ?? new List<string>());
}
