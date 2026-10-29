using Microsoft.EntityFrameworkCore;

namespace Journal.Storage;

/// <summary>
/// DbContext для Command Store (Event Sourcing) журнала.
/// Хранит доменные события в таблице DomainEvents (схема public — профиль Single).
/// </summary>
public sealed class CommandDbContext : DbContext
{
    public CommandDbContext(DbContextOptions<CommandDbContext> options) : base(options)
    {
    }

    public DbSet<DomainEventEventEntry> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .HasAnnotation("ProductVersion", "0.0.1")
            .ApplyConfiguration(new DomainEventMapping());
}
