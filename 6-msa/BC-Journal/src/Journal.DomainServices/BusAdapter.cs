using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.Extensions.Logging;

namespace Journal.DomainServices;

/// <summary>
/// Наблюдатель результата агрегата — публикует доменное событие в шину.
/// </summary>
internal class BusAdapter : IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<BusAdapter> _logger;

    public BusAdapter(IEventBus eventBus, ILogger<BusAdapter> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public void OnCompleted() => _logger.LogInformation("Поток доменных событий журнала завершён.");

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке доменных событий журнала.");

    public void OnNext(AggregateResult<IJournal, IAnemicModel<IJournal>> value)
    {
        _logger.LogInformation(
            "DE! Command={Command}; Version={Version}; Result={Result}",
            value.Event.Command.CommandName, value.Event.Version, value.Event.Result);

        _eventBus.Publish(value.Event);
    }
}
