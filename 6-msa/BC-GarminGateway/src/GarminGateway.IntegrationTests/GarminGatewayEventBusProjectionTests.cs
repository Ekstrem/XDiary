using GarminGateway.Api.Events;
using GarminGateway.Domain;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using GarminGateway.Storage;
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

namespace GarminGateway.IntegrationTests;

/// <summary>BE-140 L3: событие ЧЕРЕЗ ШИНУ → консьюмер → материализация в РЕАЛЬНОМ Postgres (`garmin_sync_read`).</summary>
public class GarminGatewayEventBusProjectionTests
{
    private readonly ITestOutputHelper _out;
    public GarminGatewayEventBusProjectionTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task Bus_Publish_Projects_RealReadModel()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — тест работает только в CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var res = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), new Credentials("key-1", "token-1", "2026-12-31"));
        Assert.True(res.IsSuccess());
        var ev = res.Event!;

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddReadStorePostgres<IGarminGateway, GarminSyncReadModel, ReadDbContext>(cs, null);
        using var sp = sc.BuildServiceProvider();

        var handler = new GarminGatewayEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        consumer.Subscribe(handler);
        var bus = sp.GetRequiredService<IEventBus>();

        bus.PublishAsync(ev);
        await Task.Delay(500);

        var repo = sp.GetRequiredService<IReadRepository<IGarminGateway, GarminSyncReadModel>>();
        var stored = await repo.GetByIdAsync(ev.Id, default);
        Assert.NotNull(stored);
        Assert.Equal("key-1", stored!.ApiKey);
        Assert.Equal("token-1", stored.OauthToken);
    }
}
