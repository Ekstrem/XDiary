using Privacy.Api.Events;
using Privacy.Domain;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
using Privacy.Storage;
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

namespace Privacy.IntegrationTests;

/// <summary>
/// BE-140 L3: событие ЧЕРЕЗ ШИНУ → консьюмер → материализация в РЕАЛЬНОМ Postgres (`pin_credential_read`).
/// Без БД — скип (в CI Postgres даёт @DO).
/// </summary>
public class PrivacyEventBusProjectionTests
{
    private readonly ITestOutputHelper _out;
    public PrivacyEventBusProjectionTests(ITestOutputHelper output) => _out = output;

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

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var res = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), "salt-1", "hash-1", 100_000);
        Assert.True(res.IsSuccess());
        var ev = res.Event!;

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddReadStorePostgres<IPrivacy, PinCredentialReadModel, ReadDbContext>(cs, null);
        using var sp = sc.BuildServiceProvider();

        var handler = new PrivacyEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        consumer.Subscribe(handler);
        var bus = sp.GetRequiredService<IEventBus>();

        bus.PublishAsync(ev);
        await Task.Delay(500);

        var repo = sp.GetRequiredService<IReadRepository<IPrivacy, PinCredentialReadModel>>();
        var stored = await repo.GetByIdAsync(ev.Id, default);
        Assert.NotNull(stored);
        Assert.Equal("salt-1", stored!.Salt);
        Assert.Equal("hash-1", stored.Hash);
        Assert.Equal(100_000, stored.Iterations);
    }
}
