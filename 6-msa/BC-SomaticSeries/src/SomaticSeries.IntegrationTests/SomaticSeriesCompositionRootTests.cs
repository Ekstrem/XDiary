using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class SomaticSeriesCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<ISomaticSeries, ISomaticSeriesAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es", o => o.EnableRetryOnFailure()));
        sc.AddScoped<SomaticSeriesEventReplay>();
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<ISomaticSeries, ISomaticSeriesAnemicModel>, SomaticSeriesEventSourcingRepository>();
        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<ISomaticSeries, ISomaticSeriesAnemicModel>>();
        Assert.IsType<SomaticSeriesEventSourcingRepository>(repo);
    }
}