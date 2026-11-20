namespace Journal.Api.Events;

using System.Diagnostics.Metrics;
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

    public JournalEventConsumer(IEventBusConsumer consumer, JournalEventStoreHandler storeHandler, JournalEventHandler handler)
    {
        _consumer = consumer;
        _handler = handler;
        _consumer.Subscribe(storeHandler);
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class JournalEventHandler : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IReadModelStore<IJournal, JournalReadModel> _readStore;
    private readonly IReadRepository<IJournal, JournalReadModel> _readRepo;
    private readonly IReadModelStore<IJournal, JournalDefaultsReadModel> _defaultsStore;
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaultsRead;

    public JournalEventHandler(
        IReadModelStore<IJournal, JournalReadModel> readStore,
        IReadRepository<IJournal, JournalReadModel> readRepo,
        IReadModelStore<IJournal, JournalDefaultsReadModel> defaultsStore,
        IReadRepository<IJournal, JournalDefaultsReadModel> defaultsRead)
    {
        _readStore = readStore;
        _readRepo = readRepo;
        _defaultsStore = defaultsStore;
        _defaultsRead = defaultsRead;
    }

    public async Task HandleAsync(IDomainEvent<IJournal> domainEvent)
    {
        Console.WriteLine("[handle] entered");
        // Канон §6: store пишет command-path (JournalCommandRepository.SaveAsync — BusAdapter «сохранить И опубликовать»),
        // здесь — только проекция MV (подписчик шины).

        // проекция MV (UpsertAsync) из changed-VO — per-field merge (BE-077/@TL 21:37, @AR 22:05 §15):
        // отсутствие ключа ≠ пропуск; применяем только имеющиеся поля, null — только при пустом diff.
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0)
        {
            Console.WriteLine("[handle] no applicable fields (skip projection)");
        }
        else
        {
            var existing = await _readRepo.GetByIdAsync(domainEvent.Id, default) ?? new JournalReadModel { Id = domainEvent.Id };
            // Канон §15.1/§8.4: идемпотентность по версии — событие старше/равное уже применённому пропускаем.
            // BE-137 (@TL 2026-11-19): у legacy-строк Version — ms-таймстамп (AnemicModel.Version по умолчанию
            // = ToUnixTimeMilliseconds()), а у события Version — 1,2,3. Тогда existing.Version >= domainEvent.Version
            // всегда истинно → read-модель не движется. Тот же пол, что в BC-Client (#2889) и BC-Partners (BE-131).
            const long LegacyTimestampFloor = 1_000_000_000_000L;
            if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor)
            {
                Console.WriteLine("[handle] stale event (version) — skip projection");
            }
            else
            {
                JournalProjection.ApplyVos(existing, vos);
                existing.Version = domainEvent.Version;
                await _readStore.UpsertAsync(existing);
            }
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
    // Канон §4: потеря события — отказ, не деградация; fire-and-forget с ВИДИМЫМ падением (счётчик + лог),
    // а не молчаливый unobserved.
    private static readonly Counter<long> EventFailures = new Meter("Journal.Events").CreateCounter<long>("event.failures", description: "unhandled event failures");

    public void OnNext(IDomainEvent<IJournal> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            EventFailures.Add(1);
            Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
        }
    }, TaskScheduler.Default);
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
            m.StartedAt = root.StartedAt.ToString("o");
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
