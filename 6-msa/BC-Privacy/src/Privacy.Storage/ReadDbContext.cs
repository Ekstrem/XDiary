namespace Privacy.Storage;
using Microsoft.EntityFrameworkCore;
using Privacy.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<PinCredentialReadModel> Items => Set<PinCredentialReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<PinCredentialReadModel>(e => { e.ToTable("pin_credential_read"); e.HasKey(x => x.Id); e.HasIndex(x => x.ClientId); }); }
}
