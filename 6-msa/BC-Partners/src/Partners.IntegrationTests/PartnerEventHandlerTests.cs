using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Api.Events;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
using XDiary.Contracts;
using Xunit;

/// <summary>
/// Материализация MV: консьюмер обязан (1) сохранить событие и (2) сделать UpsertAsync read-модели.
/// Лаг допустим, но «материализации нет вообще» — баг: если UpsertAsync не вызван, тест падает.
/// </summary>
public class PartnerEventHandlerTests
{
    private sealed class FakeStore : IEventStoreRepository<IPartners, IPartnerAnemicModel>
    {
        public int SavedEvents;
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { SavedEvents++; return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<IPartnerAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IPartnerAnemicModel>());
        public Task<IPartnerAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
        public Task<IPartnerAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
    }

    private sealed class FakeReadStore : IReadModelStore<IPartners, PartnersReadModel>
    {
        public PartnersReadModel? Upserted;
        public Task UpsertAsync(PartnersReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void Handle_PersistsEvent_And_MaterializesReadModel()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        Assert.Equal(DomainOperationResultEnum.Success, reg.Result);

        var store = new FakeStore();
        var readStore = new FakeReadStore();
        var handler = new PartnerEventHandler(store, readStore);

        handler.HandleAsync(reg.Event!).GetAwaiter().GetResult();

        Assert.Equal(1, store.SavedEvents);          // событие в event store
        Assert.NotNull(readStore.Upserted);          // MV материализована
        Assert.Equal("Алиса", readStore.Upserted!.Name);
        Assert.Equal(clientId, readStore.Upserted.ClientId);
        Assert.Null(readStore.Upserted.DeletedAt);
    }

    [Fact]
    public void Handle_RemoveEvent_MaterializesSoftDeletedReadModel()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        var removed = Aggregate.CreateInstance(reg.Model()).Remove();

        var store = new FakeStore();
        var readStore = new FakeReadStore();
        var handler = new PartnerEventHandler(store, readStore);

        handler.HandleAsync(removed.Event!).GetAwaiter().GetResult();

        Assert.NotNull(readStore.Upserted);
        Assert.NotNull(readStore.Upserted!.DeletedAt); // soft-delete доехал в MV
    }
}
