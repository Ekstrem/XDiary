using XDiary.Contracts;
using Portability.Api.Events;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Portability.DomainServices;
using Xunit;

public class PortabilityBusAdapterVersionTests
{
    [Fact]
    public async Task TwoCommands_WhenSavedTwice_ShouldProduceDistinctVersions()
    {
        var id = Guid.NewGuid();
        var r1 = Aggregate.CreateInstance(new AnemicModel { Id = id }).ImportRecords(new[] { "row-1" }, Guid.NewGuid());
        var r2 = Aggregate.CreateInstance(r1.Model()).ImportRecords(new[] { "row-1" }, Guid.NewGuid());

        var store = new CountingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IPortability, ICsvDocumentAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var handler = new PortabilityEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(r1.Event);
        await handler.HandleAsync(r2.Event);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(r1.Event.Command.Version, store.Saved[0].Version);
        Assert.Equal(r2.Event.Command.Version, store.Saved[1].Version);
        Assert.True(store.Saved[1].Version > store.Saved[0].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<IPortability, ICsvDocumentAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<ICsvDocumentAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<ICsvDocumentAnemicModel>());
        public Task<ICsvDocumentAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<ICsvDocumentAnemicModel>(null!);
        public Task<ICsvDocumentAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<ICsvDocumentAnemicModel>(null!);
    }
}