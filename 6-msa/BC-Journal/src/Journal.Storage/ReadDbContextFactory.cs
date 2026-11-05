using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Journal.Storage;

/// <summary>
/// BE-072: design-time фабрика для миграции ReadDbContext (journal_read/partners_directory/journal_defaults).
/// </summary>
public sealed class ReadDbContextFactory : IDesignTimeDbContextFactory<ReadDbContext>
{
    public ReadDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadDbContext>()
            .UseNpgsql("Host=localhost;Database=xdiary;Username=postgres;Password=postgres")
            .Options;
        return new ReadDbContext(options);
    }
}
