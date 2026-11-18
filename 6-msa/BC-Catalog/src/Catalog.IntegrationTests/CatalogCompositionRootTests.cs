using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// BE-132 composition-root: IAnemicModelRepository обязан резолвиться в наш
/// CatalogEventSourcingRepository (реальный replay), а не в библиотечный
/// FullEventSourcingRepository (GetById = TODO → пустой список → Version=0).
/// </summary>
public class CatalogCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<ICatalog, ICatalogAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es"));
        sc.AddScoped<CatalogEventReplay>();
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<ICatalog, ICatalogAnemicModel>, CatalogEventSourcingRepository>();

        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<ICatalog, ICatalogAnemicModel>>();

        Assert.IsType<CatalogEventSourcingRepository>(repo);
    }
}
