using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Api.Events;
using Journal.Domain;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using XDiary.Contracts;
using Xunit;

// BE-151 (DS ProjectionIdempotencyTests + ACO EventSourcingTests): повторная доставка,
// stale/version-conflict и идемпотентность регистров проекции Journal.
public class ProjectionIdempotencyTests
{
    private static CommandToAggregate Cmd(string name, long version)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", version);

    private static (JournalEventHandler Handler, FakeReadStore<JournalReadModel> Records, FakeReadStore<JournalDefaultsReadModel> Defaults) CreateHandler(JournalReadModel? existing = null)
    {
        var records = new FakeReadStore<JournalReadModel>();
        if (existing is not null) records.Items[existing.Id] = existing;
        var defaults = new FakeReadStore<JournalDefaultsReadModel>();
        var handler = new JournalEventHandler(records, records, defaults, defaults);
        return (handler, records, defaults);
    }

    private static IDomainEvent<IJournal> PlaceEvent(Guid id, string place, long version)
    {
        var start = Aggregate.CreateInstance(new AnemicModel { Id = id, Version = 0 }).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry", version - 1));
        return Aggregate.CreateInstance(start.Model()).SpecifyPlace(place, Cmd("SpecifyPlace", version)).Event;
    }

    [Fact]
    public async Task Projection_When_SameEventAppliedTwice_Should_UpsertOnlyOnce()
    {
        var id = Guid.NewGuid();
        var e = PlaceEvent(id, "дом", 100);
        var (handler, records, _) = CreateHandler();

        await handler.HandleAsync(e);
        await handler.HandleAsync(e);

        Assert.Equal(1, records.Upserts);
        Assert.Equal("дом", records.Items[id].Place);
    }

    [Fact]
    public async Task Projection_When_StaleEventArrives_Should_NotOverwriteNewerState()
    {
        var id = Guid.NewGuid();
        var existing = new JournalReadModel { Id = id, Place = "новое", Version = 200 };
        var (handler, records, _) = CreateHandler(existing);

        await handler.HandleAsync(PlaceEvent(id, "старое", 100));

        Assert.Equal(0, records.Upserts);
        Assert.Equal("новое", records.Items[id].Place);
        Assert.Equal(200, records.Items[id].Version);
    }

    [Fact]
    public async Task Projection_When_NewerEventArrives_Should_ApplyAndSetCommandVersion()
    {
        var id = Guid.NewGuid();
        var existing = new JournalReadModel { Id = id, Place = "старое", Version = 100 };
        var (handler, records, _) = CreateHandler(existing);

        await handler.HandleAsync(PlaceEvent(id, "новое", 200));

        Assert.Equal(1, records.Upserts);
        Assert.Equal("новое", records.Items[id].Place);
        Assert.Equal(200, records.Items[id].Version);
    }

    [Fact]
    public async Task Projection_When_DuplicateVersionConflict_Should_NotApplySecondPayload()
    {
        var id = Guid.NewGuid();
        var (handler, records, _) = CreateHandler();

        await handler.HandleAsync(PlaceEvent(id, "первый", 100));
        await handler.HandleAsync(PlaceEvent(id, "конфликт", 100));

        Assert.Equal(1, records.Upserts);
        Assert.Equal("первый", records.Items[id].Place);
    }

    [Fact]
    public async Task Defaults_When_SameAccessoriesAppliedTwice_Should_NotDuplicateRegisters()
    {
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var vos = new Dictionary<string, DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IValueObject>
        {
            ["ClientId"] = new ClientIdValueObject(clientId),
            ["Accessories"] = AccessoriesValue.Create(new[] { "c" }, new[] { "t" }, new[] { "f" }, new[] { "tag" })
        };
        var e = new DomainEvent<IJournal>(id, 0, Cmd("AddAccessories", 100), vos,
            BoundedContext<IJournal>.GetInfo(), DigiTFactory.Libraries.SeedWorks.Result.DomainOperationResultEnum.Success, "");
        var (handler, _, defaults) = CreateHandler();

        await handler.HandleAsync(e);
        await handler.HandleAsync(e);

        var model = Assert.Single(defaults.Items).Value;
        Assert.Equal(new[] { "f" }, model.Fantasies);
        Assert.Equal(new[] { "tag" }, model.Tags);
    }

    private sealed class FakeReadStore<T> : IReadModelStore<IJournal, T>, IReadRepository<IJournal, T>
        where T : class, IReadModel<IJournal>
    {
        public readonly Dictionary<Guid, T> Items = new();
        public int Upserts { get; private set; }
        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.TryGetValue(id, out var value) ? value : null);
        public Task<IReadOnlyList<T>> GetAllAsync(IPaging paging, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(Items.Values.ToList());
        public Task<long> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult((long)Items.Count);
        public Task UpsertAsync(T model, CancellationToken cancellationToken = default)
        {
            Upserts++;
            Items[IdOf(model)] = model;
            return Task.CompletedTask;
        }
        private static Guid IdOf(T model) => (Guid)model.GetType().GetProperty("Id")!.GetValue(model)!;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }
}
