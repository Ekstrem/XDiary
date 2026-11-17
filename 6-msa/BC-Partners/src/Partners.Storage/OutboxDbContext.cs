namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;

/// <summary>Transactional outbox (BE-059) — отдельный контекст от команды/событий (QG-301).</summary>
public sealed class OutboxDbContext : DbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options) { }
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e => { e.ToTable("outbox"); e.HasKey(x => x.Id); e.Property(x => x.EventType).IsRequired(); e.Property(x => x.Payload).IsRequired(); e.Property(x => x.ProcessedAt); e.Property(x => x.Attempts); });
    }
}
