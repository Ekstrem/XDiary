using XDiary.Contracts;
using Client.Api.Events;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.DomainServices;
using Xunit;

public class ClientBusAdapterVersionTests
{
    [Fact]
    public async Task TwoCommands_WhenSavedTwice_ShouldProduceDistinctVersions()
    {
        var id = Guid.NewGuid();
        var r1 = Aggregate.CreateInstance(new AnemicModel { Id = id }).UpdateProfile("Alice", "female", null);
        var r2 = Aggregate.CreateInstance(r1.Model()).UpdateProfile("Alice", "female", null);

        var store = new CountingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IClients, IClientAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var handler = new ClientEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(r1.Event);
        await handler.HandleAsync(r2.Event);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(r1.Event.Command.Version, store.Saved[0].Version);
        Assert.Equal(r2.Event.Command.Version, store.Saved[1].Version);
        Assert.True(store.Saved[1].Version > store.Saved[0].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<IClients, IClientAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IClientAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IClientAnemicModel>());
        public Task<IClientAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IClientAnemicModel>(null!);
        public Task<IClientAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IClientAnemicModel>(null!);
    }
}