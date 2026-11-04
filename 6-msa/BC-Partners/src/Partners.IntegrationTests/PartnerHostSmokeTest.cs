using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.InternalContracts;
using Partners.Storage;
using Xunit;

/// <summary>
/// Рантайм-DI smoke: EventStoreDbContext должен получить типизированный
/// DbContextOptions&lt;EventStoreDbContext&gt; (через мост AddSingleton&lt;DbContextOptions&gt;),
/// иначе DF-ctor бросает «must be a DbContextOptions&lt;EventStoreDbContext&gt;».
/// Плюс ассерт: Command/Read сохраняют СВОИ строки подключения.
/// </summary>
public class PartnerHostSmokeTest
{
    [Fact]
    public void DiGraph_Resolves_EventStoreWithTypedOptions_AndOwnConnections()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Порядок — как в Partners.Api/Program.cs.
        services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>("Host=localhost;Database=es", null);
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es"));
        services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>("Host=localhost;Database=read", null);
        services.AddDbContext<CommandDbContext>(o => o.UseNpgsql("Host=localhost;Database=cmd"));
        services.AddDbContext<ReadDbContext>(o => o.UseNpgsql("Host=localhost;Database=read"));
        // Мост: необобщённый DbContextOptions → типизированный EventStoreDbContext.
        services.AddSingleton<DbContextOptions>(sp => sp.GetRequiredService<DbContextOptions<EventStoreDbContext>>());

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var command = scope.ServiceProvider.GetRequiredService<CommandDbContext>();
        var read = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

        Assert.NotNull(eventStore);
        // @AR: Command/Read не должны подхватить Event Store строку.
        Assert.Contains("Database=cmd", command.Database.GetDbConnection().ConnectionString);
        Assert.Contains("Database=read", read.Database.GetDbConnection().ConnectionString);
    }
}
