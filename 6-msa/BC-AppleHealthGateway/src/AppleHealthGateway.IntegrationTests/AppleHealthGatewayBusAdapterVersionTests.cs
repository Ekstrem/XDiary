using AppleHealthGateway.Api.Events;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.DomainServices;
using Xunit;

public class AppleHealthGatewayBusAdapterVersionTests
{
    [Fact]
    public async Task TwoCommands_WhenSavedTwice_ShouldProduceDistinctVersions()
    {
        var id = Guid.NewGuid();
        var r1 = Aggregate.CreateInstance(new AnemicModel { Id = id }).Configure(Guid.NewGuid(), new[] { "hr", "sleep" });
        var r2 = Aggregate.CreateInstance(new AnemicModel { Id = id }).Configure(Guid.NewGuid(), new[] { "hr", "sleep" });

        var store = new CountingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var handler = new AppleHealthGatewayEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(r1.Event);
        await handler.HandleAsync(r2.Event);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(1, store.Saved[0].Version);
        Assert.Equal(2, store.Saved[1].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IAppleHealthGatewayAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IAppleHealthGatewayAnemicModel>());
        public Task<IAppleHealthGatewayAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IAppleHealthGatewayAnemicModel>(null!);
        public Task<IAppleHealthGatewayAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IAppleHealthGatewayAnemicModel>(null!);
    }
}