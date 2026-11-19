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
/// Р Р°РЅС‚Р°Р№Рј-DI smoke: EventStoreDbContext РґРѕР»Р¶РµРЅ РїРѕР»СѓС‡РёС‚СЊ С‚РёРїРёР·РёСЂРѕРІР°РЅРЅС‹Р№
/// DbContextOptions&lt;EventStoreDbContext&gt; (С‡РµСЂРµР· РјРѕСЃС‚ AddSingleton&lt;DbContextOptions&gt;),
/// РёРЅР°С‡Рµ DF-ctor Р±СЂРѕСЃР°РµС‚ В«must be a DbContextOptions&lt;EventStoreDbContext&gt;В».
/// РџР»СЋСЃ Р°СЃСЃРµСЂС‚: Command/Read СЃРѕС…СЂР°РЅСЏСЋС‚ РЎР’РћР СЃС‚СЂРѕРєРё РїРѕРґРєР»СЋС‡РµРЅРёСЏ.
/// </summary>
public class PartnerHostSmokeTest
{
    [Fact]
    public void DiGraph_Resolves_EventStoreWithTypedOptions_AndOwnConnections()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // РџРѕСЂСЏРґРѕРє вЂ” РєР°Рє РІ Partners.Api/Program.cs.
        services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>("Host=localhost;Database=es", null);
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es", o => o.EnableRetryOnFailure()));
        services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>("Host=localhost;Database=read", null);
        services.AddDbContext<OutboxDbContext>(o => o.UseNpgsql("Host=localhost;Database=cmd", o => o.EnableRetryOnFailure()));
        services.AddDbContext<ReadDbContext>(o => o.UseNpgsql("Host=localhost;Database=read", o => o.EnableRetryOnFailure()));
        // РњРѕСЃС‚: РЅРµРѕР±РѕР±С‰С‘РЅРЅС‹Р№ DbContextOptions в†’ С‚РёРїРёР·РёСЂРѕРІР°РЅРЅС‹Р№ EventStoreDbContext.
        services.AddSingleton<DbContextOptions>(sp => sp.GetRequiredService<DbContextOptions<EventStoreDbContext>>());

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var command = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var read = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

        Assert.NotNull(eventStore);
        // @AR: Command/Read РЅРµ РґРѕР»Р¶РЅС‹ РїРѕРґС…РІР°С‚РёС‚СЊ Event Store СЃС‚СЂРѕРєСѓ.
        Assert.Contains("Database=cmd", command.Database.GetDbConnection().ConnectionString);
        Assert.Contains("Database=read", read.Database.GetDbConnection().ConnectionString);
    }
}
