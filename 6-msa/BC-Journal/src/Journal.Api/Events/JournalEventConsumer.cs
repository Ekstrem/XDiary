namespace Journal.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using XDiary.Contracts;

/// <summary>
/// E1: консьюмер событий Journal. Подписывает хендлер на in-memory шину;
/// хендлер пишет событие в event store и проектирует MV (UpsertAsync).
/// </summary>
public sealed class JournalEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;
    private readonly JournalEventHandler _handler;

    public JournalEventConsumer(IEventBusConsumer consumer, JournalEventHandler handler)
    {
        _consumer = consumer;
        _handler = handler;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class JournalEventHandler : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IEventStoreRepository<IJournal, IJournalAnemicModel> _store;
    private readonly IReadModelStore<IJournal, JournalReadModel> _readStore;
    private readonly IReadRepository<IJournal, JournalReadModel> _readRepo;
    private readonly IReadModelStore<IJournal, JournalDefaultsReadModel> _defaultsStore;
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaultsRead;

    public JournalEventHandler(
        IEventStoreRepository<IJournal, IJournalAnemicModel> store,
        IReadModelStore<IJournal, JournalReadModel> readStore,
        IReadRepository<IJournal, JournalReadModel> readRepo,
        IReadModelStore<IJournal, JournalDefaultsReadModel> defaultsStore,
        IReadRepository<IJournal, JournalDefaultsReadModel> defaultsRead)
    {
        _store = store;
        _readStore = readStore;
        _readRepo = readRepo;
        _defaultsStore = defaultsStore;
        _defaultsRead = defaultsRead;
    }

    public async Task HandleAsync(IDomainEvent<IJournal> domainEvent)
    {
        Console.WriteLine("[handle] entered");
        // 1) персист события в store (идемпотентно по Id на уровне DF-репозитория).
        // @TE 04:32 — $type-дискриминатор, иначе event-sourced GetById не реконструирует IValueObject.
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        // канон §7: имя метода — из change-track (newModel.CommandName), а не Command.GetType().Name («CommandToAggregate»).
        var cmdName = domainEvent.ChangedValueObjects is not null
            && domainEvent.ChangedValueObjects.TryGetValue("CommandName", out var cn)
            && cn is StringValueObject cns
            ? cns.Value
            : (domainEvent.Command?.GetType().Name ?? string.Empty);
        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Version,
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = cmdName,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects, jsonOptions),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await _store.SaveEventAsync(entry);

        // 2) проекция MV (UpsertAsync) из changed-VO — per-field merge (BE-077/@TL 21:37, @AR 22:05 §15):
        // отсутствие ключа ≠ пропуск; применяем только имеющиеся поля, null — только при пустом diff.
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0)
        {
            Console.WriteLine("[handle] no applicable fields (skip projection)");
        }
        else
        {
            var existing = await _readRepo.GetByIdAsync(domainEvent.Id, default) ?? new JournalReadModel { Id = domainEvent.Id };
            JournalProjection.ApplyVos(existing, vos);
            await _readStore.UpsertAsync(existing);
        }

        // 3) pre-computed defaults (E1 §1.2) — read-modify-write per-client.
        await MergeDefaultsAsync(domainEvent);
    }

    private async Task MergeDefaultsAsync(IDomainEvent<IJournal> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null) return;
        var clientId = vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv ? cv.Value : null;
        if (clientId is null) return;

        var current = await _defaultsRead.GetByIdAsync(clientId.Value, default);
        var place = vos.TryGetValue("Place", out var p) && p is PlaceValue sv ? sv.Value : string.Empty;
        var positions = vos.TryGetValue("Positions", out var pp) && pp is PositionsValue pl ? pl.Value : Array.Empty<string>();
        var accessories = vos.TryGetValue("Accessories", out var ac) && ac is AccessoriesValue av ? av : null;
        var fantasies = accessories?.Fantasies ?? Array.Empty<string>();
        var tags = accessories?.Tags ?? Array.Empty<string>();
        var contraceptives = vos.TryGetValue("Contraception", out var cp) && cp is ContraceptionValue cl ? cl.Contraceptives : Array.Empty<string>();

        var defaults = current ?? new JournalDefaultsReadModel { Id = clientId.Value, ClientId = clientId.Value };
        if (!string.IsNullOrWhiteSpace(place)) defaults.LastPlace = place;
        defaults.Places = (defaults.Places ?? Array.Empty<string>()).Concat(string.IsNullOrWhiteSpace(place) ? Array.Empty<string>() : new[] { place }).Distinct().ToList();
        defaults.Fantasies = (defaults.Fantasies ?? Array.Empty<string>()).Concat(fantasies).Distinct().ToList();
        defaults.Positions = (defaults.Positions ?? Array.Empty<string>()).Concat(positions).Distinct().ToList();
        defaults.Tags = (defaults.Tags ?? Array.Empty<string>()).Concat(tags).Distinct().ToList();
        defaults.Contraceptives = (defaults.Contraceptives ?? Array.Empty<string>()).Concat(contraceptives).Distinct().ToList();

        await _defaultsStore.UpsertAsync(defaults);
    }

    // DF InMemoryEventBus.Publish кастит хендлер к IObserver<IDomainEvent<T>> (Rx) — адаптер.
    // @TE 05:04 — fire-and-forget, чтобы синхронный consumer не держал запрос (504).
    public void OnNext(IDomainEvent<IJournal> e) => _ = HandleAsync(e);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

public static class JournalProjection
{
    public static JournalReadModel? Project(IDomainEvent<IJournal> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return null; // нечего применять (пустой diff)
        var m = new JournalReadModel { Id = domainEvent.Id };
        ApplyVos(m, vos);
        return m;
    }

    // per-field: применяет только поля, которые реально есть в diff (@AR 22:05 §15 п.2).
    public static void ApplyVos(JournalReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IRecordRoot root)
        {
            m.Partners = root.Partners.ToList();
            m.StartedAt = root.StartedAt;
        }
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m.ClientId = cv.Value;
        if (vos.TryGetValue("Place", out var p) && p is PlaceValue pv) m.Place = pv.Value;
        if (vos.TryGetValue("Positions", out var po) && po is PositionsValue pov) m.Positions = pov.Value.ToList();
        if (vos.TryGetValue("Accessories", out var a) && a is AccessoriesValue av) { m.Costumes = av.Costumes.ToList(); m.Toys = av.Toys.ToList(); m.Fantasies = av.Fantasies.ToList(); m.Tags = av.Tags.ToList(); }
        if (vos.TryGetValue("Contraception", out var cn) && cn is ContraceptionValue cnv) { m.Contraceptives = cnv.Contraceptives.ToList(); m.ContraceptiveCount = cnv.ContraceptiveCount; }
        if (vos.TryGetValue("Orgasms", out var o) && o is OrgasmsListValueObject ov) m.Orgasms = ov.Value.Select(x => new OrgasmsReadModel { Participant = x.Participant, Count = x.Count, Types = x.Types.ToList(), Rating = x.Rating }).ToList();
        if (vos.TryGetValue("Origin", out var or) && or is OriginValue orv) { m.Garmin = orv.Garmin; m.GarminId = orv.GarminId; }
        if (vos.TryGetValue("Duration", out var d) && d is DurationValue dv) m.DurationMin = dv.Minutes;
        if (vos.TryGetValue("State", out var s) && s is IntValueObject sv) m.State = sv.Value;
        if (vos.TryGetValue("Deleted", out var del) && del is BoolValueObject dvb) m.Deleted = dvb.Value;
    }
}
