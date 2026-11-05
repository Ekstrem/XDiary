namespace Journal.Storage;
using System.Text.Json;
using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<RecordEntity> Records => Set<RecordEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RecordEntity>(e => { e.ToTable("records"); e.HasKey(x => x.Id); e.HasIndex(x => x.StartedAt); });

        // BE-068: read-модели должны быть в EF-модели (иначе Cannot create a DbSet).
        b.Entity<JournalReadModel>(e =>
        {
            e.ToTable("journal_read");
            e.HasKey(x => x.Id);
            e.Property(x => x.Partners).HasConversion(StringList());
            e.Property(x => x.PartnersNames).HasConversion(StringList());
            e.Property(x => x.Positions).HasConversion(StringList());
            e.Property(x => x.Tags).HasConversion(StringList());
            e.Property(x => x.Costumes).HasConversion(StringList());
            e.Property(x => x.Toys).HasConversion(StringList());
            e.Property(x => x.Fantasies).HasConversion(StringList());
            e.Property(x => x.Contraceptives).HasConversion(StringList());
            e.Property(x => x.Orgasms).HasConversion(Orgasms());
        });

        b.Entity<PartnersDirectoryReadModel>(e =>
        {
            e.ToTable("partners_directory");
            e.HasKey(x => x.PartnerId);
        });

        b.Entity<JournalDefaultsReadModel>(e =>
        {
            e.ToTable("journal_defaults");
            e.HasKey(x => x.Id);
            e.Property(x => x.Places).HasConversion(StringList());
            e.Property(x => x.Fantasies).HasConversion(StringList());
            e.Property(x => x.Positions).HasConversion(StringList());
            e.Property(x => x.Tags).HasConversion(StringList());
            e.Property(x => x.Contraceptives).HasConversion(StringList());
        });
    }

    private static ValueConverter<IReadOnlyList<string>, string> StringList()
        => new(v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions)null!),
               s => JsonSerializer.Deserialize<List<string>>(s ?? "[]", (JsonSerializerOptions)null!) ?? new List<string>());

    private static ValueConverter<IReadOnlyList<OrgasmsReadModel>, string> Orgasms()
        => new(v => JsonSerializer.Serialize(v ?? new List<OrgasmsReadModel>(), (JsonSerializerOptions)null!),
               s => JsonSerializer.Deserialize<List<OrgasmsReadModel>>(s ?? "[]", (JsonSerializerOptions)null!) ?? new List<OrgasmsReadModel>());
}
