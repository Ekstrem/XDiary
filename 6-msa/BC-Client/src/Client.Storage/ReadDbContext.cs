namespace Client.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options) { }
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<ClientEntity>(e => { e.ToTable("clients"); e.HasKey(x => x.Id); e.Property(x => x.Phone).HasMaxLength(32).IsRequired(); e.HasIndex(x => x.Phone).IsUnique(); }); }
}