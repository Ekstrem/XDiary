namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
using Partners.InternalContracts;

public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<PartnersReadModel> PartnersRead => Set<PartnersReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // BE-072: read-контекст описывает только read-таблицу (partners_read), без command-сущности PartnerEntity.
        b.Entity<PartnersReadModel>(e => { e.ToTable("partners_read"); e.HasKey(x => x.Id); });
    }
}
