using Microsoft.EntityFrameworkCore;

namespace Journal.Storage;

/// <summary>
/// DbContext РґР»СЏ Command Store (Event Sourcing) Р¶СѓСЂРЅР°Р»Р°.
/// РҐСЂР°РЅРёС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ РІ С‚Р°Р±Р»РёС†Рµ DomainEvents (СЃС…РµРјР° public вЂ” РїСЂРѕС„РёР»СЊ Single).
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
