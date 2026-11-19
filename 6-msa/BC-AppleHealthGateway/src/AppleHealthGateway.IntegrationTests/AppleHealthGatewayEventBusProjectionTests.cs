using AppleHealthGateway.Api.Events;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
using AppleHealthGateway.Storage;
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

namespace AppleHealthGateway.IntegrationTests;

/// <summary>BE-140 L3: событие ЧЕРЕЗ ШИНУ → консьюмер → материализация в РЕАЛЬНОМ Postgres (`apple_health_read`).</summary>
public class AppleHealthGatewayEventBusProjectionTests
{
    private readonly ITestOutputHelper _out;
    public AppleHealthGatewayEventBusProjectionTests(ITestOutputHelper output) => _out = output;

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

        var res = Aggregate.CreateInstance(new AnemicModel()).Configure(Guid.NewGuid(), new[] { "steps", "hr" });
        Assert.True(res.IsSuccess());
        var ev = res.Event!;

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddReadStorePostgres<IAppleHealthGateway, AppleImportReadModel, ReadDbContext>(cs, null);
        using var sp = sc.BuildServiceProvider();

        var handler = new AppleHealthGatewayEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        consumer.Subscribe(handler);
        var bus = sp.GetRequiredService<IEventBus>();

        bus.PublishAsync(ev);
        await Task.Delay(500);

        var repo = sp.GetRequiredService<IReadRepository<IAppleHealthGateway, AppleImportReadModel>>();
        var stored = await repo.GetByIdAsync(ev.Id, default);
        Assert.NotNull(stored);
        Assert.Contains("steps", stored!.Metrics);
    }
}
