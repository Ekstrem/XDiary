using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AppleHealthGatewayCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es", o => o.EnableRetryOnFailure()));
        sc.AddScoped<AppleHealthGatewayEventReplay>();
        // BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES.
        sc.AddScoped<DigiTFactory.Libraries.CommandRepository.Postgres.Repositories.IEventStreamRehydrator<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>, AppleHealthGatewayEventRehydrator>();
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>, AppleHealthGatewayEventSourcingRepository>();
        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>();
        Assert.IsType<AppleHealthGatewayEventSourcingRepository>(repo);
    }
}