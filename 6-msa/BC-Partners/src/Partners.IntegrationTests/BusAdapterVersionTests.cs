using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.DomainServices;
using Xunit;

/// <summary>
/// BE-130 (23505 PK_DomainEvents): BusAdapter обязан присваивать событиям МОНОТОННО
/// растущие версии из хранилища (GetEventCountAsync + 1), а не result.Event.Version
/// (которое Clone копирует без инкремента). Иначе вторая команда даёт тот же (Id, Version),
/// что и первая, и PostgreSQL падает по PK.
/// </summary>
public class BusAdapterVersionTests
{
    [Fact]
    public void TwoCommands_ProduceDistinctVersions()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        var linked = Aggregate.CreateInstance(reg.Model()).LinkClient(clientId);

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        using var sp = sc.BuildServiceProvider();

        var store = new CountingStore();
        var adapter = new BusAdapter(store, sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.Events.IEventBus>());

        adapter.OnNext(reg);     // Version = 0 + 1 = 1
        adapter.OnNext(linked);  // Version = 1 + 1 = 2

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(1, store.Saved[0].Version);
        Assert.Equal(2, store.Saved[1].Version);
        Assert.Equal(store.Saved[0].Id, store.Saved[1].Id); // тот же агрегат
    }

    private sealed class CountingStore : IEventStoreRepository<IPartners, IPartnerAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IPartnerAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IPartnerAnemicModel>());
        public Task<IPartnerAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
        public Task<IPartnerAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
    }
}
