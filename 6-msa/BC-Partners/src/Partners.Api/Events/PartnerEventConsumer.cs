namespace Partners.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
using XDiary.Contracts;

/// <summary>
/// E1: консьюмер событий Partners (по конвейеру Journal).
/// Хендлер пишет событие в event store и проектирует MV (UpsertAsync).
/// </summary>
public sealed class PartnerEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public PartnerEventConsumer(IEventBusConsumer consumer, PartnerEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class PartnerEventHandler : IDomainEventHandler<IPartners>, IObserver<IDomainEvent<IPartners>>
{
    private readonly IEventStoreRepository<IPartners, IPartnerAnemicModel> _store;
    private readonly IReadModelStore<IPartners, PartnersReadModel> _readStore;
    private readonly IReadRepository<IPartners, PartnersReadModel> _readRepo;
    private readonly IPartnerEventPublisher _publisher;
    private readonly IAnemicModelRepository<IPartners, IPartnerAnemicModel> _events;

    public PartnerEventHandler(
        IEventStoreRepository<IPartners, IPartnerAnemicModel> store,
        IReadModelStore<IPartners, PartnersReadModel> readStore,
        IReadRepository<IPartners, PartnersReadModel> readRepo,
        IPartnerEventPublisher publisher,
        IAnemicModelRepository<IPartners, IPartnerAnemicModel> events)
    {
        _store = store;
        _readStore = readStore;
        _readRepo = readRepo;
        _publisher = publisher;
        _events = events;
    }

    public async Task HandleAsync(IDomainEvent<IPartners> domainEvent)
    {
        Console.WriteLine("[handle] entered");
        // @TE 04:32 — ChangedValueObjectsJson пишется с $type-дискриминатором, иначе
        // event-sourced GetById (IAnemicModelRepository) не может реконструировать IValueObject.
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

        var read = PartnerProjection.Project(domainEvent);
        if (read is not null)
        {
            // TE-102: Project отдаёт только изменённые поля (nullable); upsert без merge затирает остальное.
            var existing = await _readRepo.GetByIdAsync(domainEvent.Id, default);
            if (existing is not null)
            {
                if (read.Name is not null) existing.Name = read.Name;
                if (read.Photo is not null) existing.Photo = read.Photo;
                if (read.Gender is not null) existing.Gender = read.Gender;
                if (read.Notes is not null) existing.Notes = read.Notes;
                if (read.ClientId is not null) existing.ClientId = read.ClientId;
                if (read.DeletedAt is not null) existing.DeletedAt = read.DeletedAt;
                await _readStore.UpsertAsync(existing);
                // BE-082: публикуем текущее состояние партнёра в Journal (partners_directory).
                await _publisher.PublishAsync(existing.Id, existing.Name, existing.ClientId, existing.DeletedAt, CancellationToken.None);
            }
            else
            {
                // @AR 12:15: не материализуем строку из частичной дельты — если Root не в дельте
                // (например profile пришёл раньше register), пересобираем полное состояние из event store.
                var name = read.Name;
                var clientId = read.ClientId;
                var deletedAt = read.DeletedAt;
                if (read.Name is null)
                {
                    var agg = (await _events.GetById(domainEvent.Id, default)).LastOrDefault();
                    if (agg is not null)
                    {
                        name = agg.Root.Name;
                        clientId = agg.ClientId;
                        deletedAt = agg.DeletedAt;
                    }
                }
                var created = new PartnersReadModel
                {
                    Id = domainEvent.Id,
                    Name = name ?? string.Empty,
                    Photo = read.Photo ?? string.Empty,
                    Gender = read.Gender ?? string.Empty,
                    Notes = read.Notes ?? string.Empty,
                    ClientId = clientId,
                    DeletedAt = deletedAt,
                };
                await _readStore.UpsertAsync(created);
                await _publisher.PublishAsync(created.Id, created.Name, created.ClientId, created.DeletedAt, CancellationToken.None);
            }
        }
    }

    // DF InMemoryEventBus.Publish кастит хендлер к IObserver<IDomainEvent<T>> (Rx) —
    // без этого Handle не вызывается (InvalidCastException глушится). Адаптер:
    // @TE 05:04 — fire-and-forget, чтобы синхронный consumer не держал запрос (504).
    public void OnNext(IDomainEvent<IPartners> e) => _ = HandleAsync(e);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

public static class PartnerProjection
{
    /// <summary>DIFF проекции: null в поле = «не менялось» (TE-102 — не затираем пустым).</summary>
    public sealed record Diff(string? Name, string? Photo, string? Gender, string? Notes, Guid? ClientId, DateTime? DeletedAt);

    public static Diff? Project(IDomainEvent<IPartners> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return null;
        string? name = null, photo = null, gender = null, notes = null;
        Guid? clientId = null; DateTime? deletedAt = null;

        if (vos.TryGetValue("Root", out var r) && r is IPartnerRoot root) name = root.Name;
        if (vos.TryGetValue("Photo", out var p) && p is StringValueObject pv) photo = pv.Value;
        if (vos.TryGetValue("Gender", out var g) && g is StringValueObject gv) gender = gv.Value;
        if (vos.TryGetValue("Notes", out var n) && n is StringValueObject nv) notes = nv.Value;
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) clientId = cv.Value;
        if (vos.TryGetValue("DeletedAt", out var d) && d is DeletedAtValueObject dv) deletedAt = dv.Value;

        return new Diff(name, photo, gender, notes, clientId, deletedAt);
    }
}
