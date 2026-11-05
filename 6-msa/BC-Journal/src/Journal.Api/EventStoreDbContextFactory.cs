using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Journal.Api;

/// <summary>
/// BE-075: design-time фабрика для миграции EventStoreDbContext (Commands.DomainEvents).
/// </summary>
public sealed class EventStoreDbContextFactory : IDesignTimeDbContextFactory<EventStoreDbContext>
{
    public EventStoreDbContext CreateDbContext(string[] args)
    {
        var connection = "Host=localhost;Database=xdiary;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseNpgsql(connection, x => x.MigrationsAssembly("Journal.Storage"))
            .Options;
        return new EventStoreDbContext(options, new EventStoreOptions());
    }
}
