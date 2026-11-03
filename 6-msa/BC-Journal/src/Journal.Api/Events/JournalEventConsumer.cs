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
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class JournalEventHandler : IDomainEventHandler<IJournal>
{
    private readonly IEventStoreRepository<IJournal, IJournalAnemicModel> _store;
    private readonly IReadModelStore<IJournal, JournalReadModel> _readStore;

    public JournalEventHandler(
        IEventStoreRepository<IJournal, IJournalAnemicModel> store,
        IReadModelStore<IJournal, JournalReadModel> readStore)
    {
        _store = store;
        _readStore = readStore;
    }

    public void Handle(IDomainEvent<IJournal> domainEvent)
    {
        // 1) персист события в store (идемпотентно по Id на уровне DF-репозитория).
        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Version,
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = domainEvent.Command?.GetType().Name ?? string.Empty,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        _store.SaveEventAsync(entry).GetAwaiter().GetResult();

        // 2) проекция MV (UpsertAsync) из changed-VO.
        var read = JournalProjection.Project(domainEvent);
        if (read is not null)
            _readStore.UpsertAsync(read).GetAwaiter().GetResult();
    }
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
