using DigiTFactory.Libraries.SeedWorks.Events;
using DailyMetrics.Domain;
using DailyMetrics.DomainServices;
using Microsoft.Extensions.Logging;

namespace DailyMetrics.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ DailyMetricsProjectionHandler.
/// </summary>
public class DailyMetricsCommandDbProjector : IDomainEventHandler<IDailyMetrics>, IObserver<IDomainEvent<IDailyMetrics>>
{
    private readonly IRepository _repository;
    private readonly ILogger<DailyMetricsCommandDbProjector> _logger;

    public DailyMetricsCommandDbProjector(IRepository repository, ILogger<DailyMetricsCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IDailyMetrics> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
