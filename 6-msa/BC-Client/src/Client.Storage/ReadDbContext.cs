namespace Client.Storage;
using Microsoft.EntityFrameworkCore;
using Client.InternalContracts;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<ClientReadModel> Items => Set<ClientReadModel>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<ClientReadModel>(e => { e.ToTable("client_read"); e.HasKey(x => x.Id); }); }
}
