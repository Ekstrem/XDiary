namespace Journal.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>
/// QG-104: BusAdapter — наблюдатель за результатами Notifier'а. И сохраняет событие (SaveEventAsync),
/// и публикует его в шину (IEventBus.Publish).
/// </summary>
public sealed class BusAdapter : IObserver<AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IEventBus _eventBus;

    public BusAdapter(IEventBus eventBus) => _eventBus = eventBus;

    public void OnNext(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        if (!result.IsSuccess())
            return;

        SaveEventAsync(result).GetAwaiter().GetResult();
        Publish(result);
    }

    public void OnError(Exception error) { }

    public void OnCompleted() { }

    // Сохранение события в event store (command-path).
    private Task SaveEventAsync(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        // TODO(эталон): перенести сюда сохранение DomainEventEntry из JournalHandlers.SaveAsync
        // (сейчас событие пишется императивно в command-path хендлеров).
        return Task.CompletedTask;
    }

    // Публикация доменного события в шину.
    private void Publish(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        // TODO(эталон): построить IDomainEvent<IJournal> из результата и вызвать _eventBus.PublishAsync(domainEvent).
        _ = _eventBus;
    }
}
