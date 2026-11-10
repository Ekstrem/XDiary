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
        public List<IPartnerAnemicModel> Events = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { SavedEvents++; return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<IPartnerAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(Events);
        public Task<IPartnerAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
        public Task<IPartnerAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
    }

    private sealed class FakeReadStore : IReadModelStore<IPartners, PartnersReadModel>
    {
        public PartnersReadModel? Upserted;
        public Task UpsertAsync(PartnersReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeRead : IReadRepository<IPartners, PartnersReadModel>
    {
        public Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PartnersReadModel?>(null);
        public Task<IReadOnlyList<PartnersReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PartnersReadModel>>(new List<PartnersReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakePublisher : IPartnerEventPublisher
    {
        public int Published;
        public string? LastName;
        public Guid? LastClientId;
        public DateTime? LastDeletedAt;
        public Task PublishAsync(Guid partnerId, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default)
        { Published++; LastName = name; LastClientId = clientId; LastDeletedAt = deletedAt; return Task.CompletedTask; }
    }

    [Fact]
    public void Handle_PersistsEvent_And_MaterializesReadModel()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        Assert.Equal(DomainOperationResultEnum.Success, reg.Result);

        var store = new FakeStore();
        var readStore = new FakeReadStore();
        var handler = new PartnerEventHandler(store, readStore, new FakeRead(), new FakePublisher(), store);

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
        var handler = new PartnerEventHandler(store, readStore, new FakeRead(), new FakePublisher(), store);

        handler.HandleAsync(removed.Event!).GetAwaiter().GetResult();

        Assert.NotNull(readStore.Upserted);
        Assert.NotNull(readStore.Upserted!.DeletedAt); // soft-delete доехал в MV
    }

    // @AR 12:47: детерминированный кейс TE-102 (адрес 3) — частичное событие (profile) приходит
    // раньше материализации строки: имя обязано взяться из event store, а не быть пустым.
    [Fact]
    public void Handle_ProfileFirst_DoesNotWipeName()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        var profile = Aggregate.CreateInstance(reg.Model()).UpdateProfile("photo.jpg", "male", "note");
        Assert.Equal(DomainOperationResultEnum.Success, profile.Result);

        var store = new FakeStore { Events = new List<IPartnerAnemicModel> { reg.Model() } }; // предыдущее состояние в event store
        var readStore = new FakeReadStore();
        var publisher = new FakePublisher();
        var handler = new PartnerEventHandler(store, readStore, new FakeRead(), publisher, store);

        handler.HandleAsync(profile.Event!).GetAwaiter().GetResult();

        Assert.NotNull(readStore.Upserted);
        Assert.Equal("Алиса", readStore.Upserted!.Name);      // имя из предыдущего состояния, не ""
        Assert.Equal("male", readStore.Upserted.Gender);
        Assert.Equal("photo.jpg", readStore.Upserted.Photo);
        Assert.Equal("note", readStore.Upserted.Notes);
        // @AR 12:51: проверяем и второй канал — в Journal ушло имя, а не "".
        Assert.Equal(1, publisher.Published);
        Assert.Equal("Алиса", publisher.LastName);
        Assert.Equal(clientId, publisher.LastClientId);
    }

    // @TL 15:58: остаток (порядок + fire-and-forget) — если пересобирать нечего (Events пуст),
    // строку НЕ материализуем и пустое имя НЕ публикуем.
    [Fact]
    public void Handle_ProfileFirst_NoPreviousState_DoesNotPublishEmptyName()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        var profile = Aggregate.CreateInstance(reg.Model()).UpdateProfile("photo.jpg", "male", "note");

        var store = new FakeStore { Events = new List<IPartnerAnemicModel>() }; // пересобирать нечего
        var readStore = new FakeReadStore();
        var publisher = new FakePublisher();
        var handler = new PartnerEventHandler(store, readStore, new FakeRead(), publisher, store);

        handler.HandleAsync(profile.Event!).GetAwaiter().GetResult();

        Assert.Null(readStore.Upserted);       // полноты нет — строку не материализуем
        Assert.Equal(0, publisher.Published);  // и пустое имя НЕ ушло в Journal
    }
}
