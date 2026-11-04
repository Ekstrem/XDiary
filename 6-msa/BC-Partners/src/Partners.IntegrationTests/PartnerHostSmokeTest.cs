using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Configuration;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.InternalContracts;
using Partners.Storage;
using Xunit;

/// <summary>
/// BE-061/рантайм-DI smoke: DI-граф Partners должен резолвить EventStoreDbContext
/// и ReadDbContext с типизированными опциями (иначе необобщённый DbContextOptions
/// при двух контекстах даёт InvalidOperationException на старте).
/// </summary>
public class PartnerHostSmokeTest
{
    [Fact]
    public void DiGraph_Resolves_EventStoreAndReadDbContext_WithTypedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Порядок и набор — как в Partners.Api/Program.cs (типизированная регистрация EventStoreDbContext ДО DF).
        services.AddSingleton<EventStoreOptions>();
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es"));
        services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>("Host=localhost;Database=es", null);
        services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>("Host=localhost;Database=read", null);
        services.AddDbContext<CommandDbContext>(o => o.UseNpgsql("Host=localhost;Database=cmd"));
        services.AddDbContext<ReadDbContext>(o => o.UseNpgsql("Host=localhost;Database=read"));

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var read = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

        Assert.NotNull(eventStore);
        Assert.NotNull(read);
    }
}
