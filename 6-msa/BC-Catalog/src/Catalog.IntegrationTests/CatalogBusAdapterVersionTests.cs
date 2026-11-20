using Catalog.Api.Events;
using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.DomainServices;
using Xunit;

/// <summary>
/// BE-132: EventStoreHandler обязан присваивать событиям монотонные версии из хранилища
/// (GetEventCountAsync + 1), а не result.Event.Version (иначе 23505 PK_DomainEvents).
/// </summary>
public class CatalogBusAdapterVersionTests
{
    [Fact]
    public async Task TwoCommands_WhenSavedTwice_ShouldProduceDistinctVersions()
    {
        var clientId = Guid.NewGuid();
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", clientId);
        var rename = Aggregate.CreateInstance(start.Model()).Rename("Big Ball");

        var store = new CountingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<ICatalog, ICatalogAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var handler = new CatalogEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(start.Event);
        await handler.HandleAsync(rename.Event);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(1, store.Saved[0].Version);
        Assert.Equal(2, store.Saved[1].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<ICatalog, ICatalogAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<ICatalogAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<ICatalogAnemicModel>());
        public Task<ICatalogAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<ICatalogAnemicModel>(null!);
        public Task<ICatalogAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<ICatalogAnemicModel>(null!);
    }
}
