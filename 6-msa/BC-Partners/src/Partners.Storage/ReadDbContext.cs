namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
using Partners.InternalContracts;

public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<PartnerEntity> Partners => Set<PartnerEntity>();
    public DbSet<PartnersReadModel> PartnersRead => Set<PartnersReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PartnerEntity>(e => { e.ToTable("partners"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(256).IsRequired(); });
        // BE-068/read-store: PartnersReadModel должна быть в EF-модели (иначе Cannot create a DbSet).
        b.Entity<PartnersReadModel>(e => { e.ToTable("partners_read"); e.HasKey(x => x.Id); });
    }
}
