using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class DailyMetricsCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<IDailyMetrics, IDailyMetricsAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es", o => o.EnableRetryOnFailure()));
        sc.AddScoped<DailyMetricsEventReplay>();
        // BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES.
        sc.AddScoped<DigiTFactory.Libraries.CommandRepository.Postgres.Repositories.IEventStreamRehydrator<IDailyMetrics, IDailyMetricsAnemicModel>, DailyMetricsEventRehydrator>();
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel>, DailyMetricsEventSourcingRepository>();
        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel>>();
        Assert.IsType<DailyMetricsEventSourcingRepository>(repo);
    }
}