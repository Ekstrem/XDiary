using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.DomainServices;
using Xunit;

public class GarminGatewayBusAdapterVersionTests
{
    [Fact]
    public void TwoCommands_ProduceDistinctVersions()
    {
        var id = Guid.NewGuid();
        var r1 = Aggregate.CreateInstance(new AnemicModel { Id = id }).Connect(Guid.NewGuid(), Credentials.CreateInstance("GCDC_x", "token-1", "2026-11-01T00:00"));
        var r2 = Aggregate.CreateInstance(new AnemicModel { Id = id }).Connect(Guid.NewGuid(), Credentials.CreateInstance("GCDC_x", "token-1", "2026-11-01T00:00"));

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        using var sp = sc.BuildServiceProvider();

        var store = new CountingStore();
        var adapter = new BusAdapter(store, sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.Events.IEventBus>());

        adapter.OnNext(r1);
        adapter.OnNext(r2);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(1, store.Saved[0].Version);
        Assert.Equal(2, store.Saved[1].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<IGarminGateway, IGarminGatewayAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IGarminGatewayAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IGarminGatewayAnemicModel>());
        public Task<IGarminGatewayAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IGarminGatewayAnemicModel>(null!);
        public Task<IGarminGatewayAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IGarminGatewayAnemicModel>(null!);
    }
}