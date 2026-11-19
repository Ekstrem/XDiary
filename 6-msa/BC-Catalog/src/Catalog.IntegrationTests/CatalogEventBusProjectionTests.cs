using Catalog.Api.Events;
using Catalog.Domain;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using Catalog.Storage;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-140 L3: событие ЧЕРЕЗ ШИНУ доходит до консьюмера и материализует read-модель в РЕАЛЬНОМ Postgres
/// (класс BE-134/138: «нет консьюмера» / «проекция не пишет»). Без БД — скип (в CI Postgres даёт @DO).
/// </summary>
public class CatalogEventBusProjectionTests
{
    private readonly ITestOutputHelper _out;
    public CatalogEventBusProjectionTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task Bus_Publish_Projects_RealReadModel()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — тест шины/проекции работает только в CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs, o => o.EnableRetryOnFailure()).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var reg = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        Assert.True(reg.IsSuccess());
        var ev = reg.Event!;

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddReadStorePostgres<ICatalog, CatalogReadModel, ReadDbContext>(cs, null);
        using var sp = sc.BuildServiceProvider();

        var handler = new CatalogEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        consumer.Subscribe(handler);
        var bus = sp.GetRequiredService<IEventBus>();

        bus.PublishAsync(ev);
        await Task.Delay(500);

        var repo = sp.GetRequiredService<IReadRepository<ICatalog, CatalogReadModel>>();
        var stored = await repo.GetByIdAsync(ev.Id, default);
        Assert.NotNull(stored);
        Assert.Equal("Ball", stored!.Name);
        Assert.Equal("toy", stored.Kind);
    }
}
