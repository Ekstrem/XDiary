namespace GarminGateway.Storage;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GarminGateway.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<GarminSyncReadModel> Items => Set<GarminSyncReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GarminSyncReadModel>(e =>
        {
            e.ToTable("garmin_sync_read");
            e.HasKey(x => x.Id);
            e.Property(x => x.Metrics).HasConversion(StringList());
        });
    }
    private static ValueConverter<IReadOnlyList<string>, string> StringList()
        => new(v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions)null!),
               s => JsonSerializer.Deserialize<List<string>>(s ?? "[]", (JsonSerializerOptions)null!) ?? new List<string>());
}
