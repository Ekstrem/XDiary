namespace AppleHealthGateway.DomainServices;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>Наблюдатель за результатами Notifier'а (QG-104): сохраняет событие + публикует в шину.</summary>
public sealed class BusAdapter : IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>
{
    private readonly IEventStoreRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> _store;
    private readonly IEventBus _bus;
    public BusAdapter(IEventStoreRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> store, IEventBus bus) { _store = store; _bus = bus; }

    public void OnNext(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> result)
    {
        if (!result.IsSuccess()) return;
        SaveEventAsync(result).GetAwaiter().GetResult();
        _bus.PublishAsync(result.Event);
    }
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    private Task SaveEventAsync(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> result)
    {
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        var entry = new DomainEventEntry
        {
            Id = result.Event.Id,
            Version = result.Event.Version,
            CorrelationToken = result.Event.CorrelationToken,
            BoundedContext = result.Event.ContextName,
            CommandName = result.Event.Command?.GetType().Name ?? string.Empty,
            SubjectName = result.Event.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(result.Event.ChangedValueObjects, jsonOptions),
            Result = result.Event.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        return _store.SaveEventAsync(entry);
    }
}
