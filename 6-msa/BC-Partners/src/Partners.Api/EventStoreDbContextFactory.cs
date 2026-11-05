using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Partners.Api;

/// <summary>
/// BE-075: design-time фабрика для миграции EventStoreDbContext (Commands.DomainEvents).
/// Конструирует с типизированным DbContextOptions&lt;EventStoreDbContext&gt; (обход
/// «must be a DbContextOptions&lt;EventStoreDbContext&gt;» из не-generic ctor DF).
/// </summary>
public sealed class EventStoreDbContextFactory : IDesignTimeDbContextFactory<EventStoreDbContext>
{
    public EventStoreDbContext CreateDbContext(string[] args)
    {
        var connection = "Host=localhost;Database=xdiary;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseNpgsql(connection, x => x.MigrationsAssembly("Partners.Storage"))
            .Options;
        return new EventStoreDbContext(options, new EventStoreOptions());
    }
}
