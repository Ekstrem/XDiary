namespace Journal.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
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
    private readonly IReadModelStore<IJournal, JournalDefaultsReadModel> _defaultsStore;
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaultsRead;

    public JournalEventHandler(
        IEventStoreRepository<IJournal, IJournalAnemicModel> store,
        IReadModelStore<IJournal, JournalReadModel> readStore,
        IReadModelStore<IJournal, JournalDefaultsReadModel> defaultsStore,
        IReadRepository<IJournal, JournalDefaultsReadModel> defaultsRead)
    {
        _store = store;
        _readStore = readStore;
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
        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Version,
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = domainEvent.Command?.GetType().Name ?? string.Empty,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects, jsonOptions),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await _store.SaveEventAsync(entry);

        // 2) проекция MV (UpsertAsync) из changed-VO.
        var read = JournalProjection.Project(domainEvent);
        if (read is not null)
            await _readStore.UpsertAsync(read);

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
        var place = vos.TryGetValue("Place", out var p) && p is StringValueObject sv ? sv.Value : string.Empty;
        var fantasies = vos.TryGetValue("Fantasies", out var f) && f is StringListValueObject fl ? fl.Value : Array.Empty<string>();
        var positions = vos.TryGetValue("Positions", out var pp) && pp is StringListValueObject pl ? pl.Value : Array.Empty<string>();
        var tags = vos.TryGetValue("Tags", out var tg) && tg is StringListValueObject tl ? tl.Value : Array.Empty<string>();
        var contraceptives = vos.TryGetValue("Contraceptives", out var cp) && cp is StringListValueObject cl ? cl.Value : Array.Empty<string>();

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
        if (vos is null) return null;

        var root = vos.TryGetValue("Root", out var r) ? r as IRecordRoot : null;
        if (root is null) return null;

        return new JournalReadModel
        {
            Id = domainEvent.Id,
            ClientId = vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv ? cv.Value : null,
            Partners = root.Partners.ToList(),
            StartedAt = root.StartedAt,
        };
    }
}
