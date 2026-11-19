using Client.Api.Events;
using Client.Domain;
using Client.Domain.Implementation;
using Client.InternalContracts;
using Client.Storage;
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

namespace Client.IntegrationTests;

/// <summary>BE-140 L3: событие ЧЕРЕЗ ШИНУ → консьюмер → материализация в РЕАЛЬНОМ Postgres (`client_read`).</summary>
public class ClientEventBusProjectionTests
{
    private const string Phone = "+79000000000";
    private readonly ITestOutputHelper _out;
    public ClientEventBusProjectionTests(ITestOutputHelper output) => _out = output;

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

        var res = Aggregate.CreateInstance(new AnemicModel()).Register(Phone, "123456");
        Assert.True(res.IsSuccess());
        var ev = res.Event!;

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddReadStorePostgres<IClients, ClientReadModel, ReadDbContext>(cs, null);
        using var sp = sc.BuildServiceProvider();

        var handler = new ClientEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        consumer.Subscribe(handler);
        var bus = sp.GetRequiredService<IEventBus>();

        bus.PublishAsync(ev);
        await Task.Delay(500);

        var repo = sp.GetRequiredService<IReadRepository<IClients, ClientReadModel>>();
        var stored = await repo.GetByIdAsync(ev.Id, default);
        Assert.NotNull(stored);
        Assert.Equal(Phone, stored!.Phone);
    }
}
