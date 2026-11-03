namespace Journal.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;

/// <summary>
/// E1: консьюмер событий Journal. Подписывает хендлер на in-memory шину;
/// хендлер пишет событие в event store и проектирует MV (UpsertAsync).
/// </summary>
public sealed class JournalEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public JournalEventConsumer(IEventBusConsumer consumer)
    {
        _consumer = consumer;
        _consumer.Subscribe(new JournalEventHandler());
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class JournalEventHandler : IDomainEventHandler<IJournal>
{
    public void Handle(IDomainEvent<IJournal> domainEvent)
    {
        // TODO(E1): SaveEventAsync(DomainEventEntry) + UpsertAsync(JournalReadModel)
    }
}
