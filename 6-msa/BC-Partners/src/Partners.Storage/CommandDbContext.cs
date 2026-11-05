namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;
public sealed class CommandDbContext : DbContext
{
    public CommandDbContext(DbContextOptions<CommandDbContext> options) : base(options) { }
    public DbSet<PartnerEntity> Partners => Set<PartnerEntity>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PartnerEntity>(e => { e.ToTable("partners"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(256).IsRequired(); e.Property(x => x.ClientId); });
        // BE-059: transactional outbox — событие пишется сюда в той же транзакции.
        b.Entity<OutboxMessage>(e => { e.ToTable("outbox"); e.HasKey(x => x.Id); e.Property(x => x.EventType).IsRequired(); e.Property(x => x.Payload).IsRequired(); e.Property(x => x.ProcessedAt); e.Property(x => x.Attempts); });
    }
}