using DigiTFactory.Libraries.SeedWorks.Events;
using SomaticSeries.Domain;
using SomaticSeries.DomainServices;
using Microsoft.Extensions.Logging;

namespace SomaticSeries.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ SomaticSeriesProjectionHandler.
/// </summary>
public class SomaticSeriesCommandDbProjector : IDomainEventHandler<ISomaticSeries>, IObserver<IDomainEvent<ISomaticSeries>>
{
    private readonly IRepository _repository;
    private readonly ILogger<SomaticSeriesCommandDbProjector> _logger;

    public SomaticSeriesCommandDbProjector(IRepository repository, ILogger<SomaticSeriesCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<ISomaticSeries> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
