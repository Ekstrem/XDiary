namespace Privacy.Storage;
using Microsoft.EntityFrameworkCore;
public sealed class CommandDbContext : DbContext
{
    public CommandDbContext(DbContextOptions<CommandDbContext> options) : base(options) { }
    public DbSet<PinCredentialEntity> Credentials => Set<PinCredentialEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    { b.Entity<PinCredentialEntity>(e => { e.ToTable("pin_credential"); e.HasKey(x => x.Id); e.HasIndex(x => x.SourceId).IsUnique(); e.Property(x => x.SourceId).HasMaxLength(32).IsRequired(); }); }
}