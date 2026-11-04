using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.InternalContracts;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Рантайм-DI smoke (зеркало Partners): EventStoreDbContext получает типизированные
/// DbContextOptions через мост; Command/Read сохраняют свои строки.
/// </summary>
public class JournalHostSmokeTest
{
    [Fact]
    public void DiGraph_Resolves_EventStoreWithTypedOptions_AndOwnConnections()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventStorePostgres<IJournal, IJournalAnemicModel>("Host=localhost;Database=es", null);
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es"));
        services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>("Host=localhost;Database=read", null);
        services.AddDbContext<CommandDbContext>(o => o.UseNpgsql("Host=localhost;Database=cmd"));
        services.AddDbContext<ReadDbContext>(o => o.UseNpgsql("Host=localhost;Database=read"));
        services.AddSingleton<DbContextOptions>(sp => sp.GetRequiredService<DbContextOptions<EventStoreDbContext>>());

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var command = scope.ServiceProvider.GetRequiredService<CommandDbContext>();
        var read = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

        Assert.NotNull(eventStore);
        Assert.Contains("Database=cmd", command.Database.GetDbConnection().ConnectionString);
        Assert.Contains("Database=read", read.Database.GetDbConnection().ConnectionString);
    }
}
