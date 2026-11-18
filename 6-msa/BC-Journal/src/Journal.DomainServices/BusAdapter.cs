namespace Journal.DomainServices;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// QG-104: BusAdapter — наблюдатель за результатами Notifier'а. Сохраняет событие (SaveEventAsync)
/// и публикует его в шину (IEventBus.PublishAsync).
/// </summary>
public sealed class BusAdapter : IObserver<AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IEventStoreRepository<IJournal, IJournalAnemicModel> _store;
    private readonly IEventBus _bus;

    public BusAdapter(IEventStoreRepository<IJournal, IJournalAnemicModel> store, IEventBus bus)
    {
        _store = store;
        _bus = bus;
    }

    public void OnNext(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        if (!result.IsSuccess())
            return;

        SaveEventAsync(result).GetAwaiter().GetResult();
        _bus.PublishAsync(result.Event);
    }

    public void OnError(Exception error) { }

    public void OnCompleted() { }

    private async Task SaveEventAsync(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        // Перенесено из JournalCommandRepository.SaveAsync: событие персистится ДО публикации
        // (BusAdapter «сохранить И опубликовать»), а не пишется consumer'ом fire-and-forget.
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        var cmdName = result.Event.ChangedValueObjects is not null
            && result.Event.ChangedValueObjects.TryGetValue("CommandName", out var cn)
            && cn is StringValueObject cns
            ? cns.Value
            : (result.Event.Command?.GetType().Name ?? string.Empty);
        var entry = new DomainEventEntry
        {
            Id = result.Event.Id,
            Version = await _store.GetEventCountAsync(result.Event.Id) + 1,
            CorrelationToken = result.Event.CorrelationToken,
            BoundedContext = result.Event.ContextName,
            CommandName = cmdName,
            SubjectName = result.Event.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(result.Event.ChangedValueObjects, jsonOptions),
            Result = result.Event.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await _store.SaveEventAsync(entry);
    }
}
