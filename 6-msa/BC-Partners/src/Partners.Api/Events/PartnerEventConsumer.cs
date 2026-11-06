namespace Partners.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;

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

    public PartnerEventHandler(
        IEventStoreRepository<IPartners, IPartnerAnemicModel> store,
        IReadModelStore<IPartners, PartnersReadModel> readStore)
    {
        _store = store;
        _readStore = readStore;
    }

    public void Handle(IDomainEvent<IPartners> domainEvent)
    {
        Console.WriteLine("[handle] entered");
        // @TE 04:32 — ChangedValueObjectsJson пишется с $type-дискриминатором, иначе
        // event-sourced GetById (IAnemicModelRepository) не может реконструировать IValueObject.
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new Partners.Storage.Outbox.IValueObjectJsonConverter());
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
        _store.SaveEventAsync(entry).GetAwaiter().GetResult();

        var read = PartnerProjection.Project(domainEvent);
        if (read is not null)
            _readStore.UpsertAsync(read).GetAwaiter().GetResult();
    }

    // DF InMemoryEventBus.Publish кастит хендлер к IObserver<IDomainEvent<T>> (Rx) —
    // без этого Handle не вызывается (InvalidCastException глушится). Адаптер:
    public void OnNext(IDomainEvent<IPartners> e) => Handle(e);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

public static class PartnerProjection
{
    public static PartnersReadModel? Project(IDomainEvent<IPartners> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null) return null;
        var root = vos.TryGetValue("Root", out var r) ? r as IPartnerRoot : null;
        var clientId = vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv ? cv.Value : null;
        var deletedAt = vos.TryGetValue("DeletedAt", out var d) && d is DeletedAtValueObject dv ? dv.Value : null;

        return new PartnersReadModel
        {
            Id = domainEvent.Id,
            Name = root?.Name ?? string.Empty,
            ClientId = clientId,
            DeletedAt = deletedAt,
        };
    }
}
