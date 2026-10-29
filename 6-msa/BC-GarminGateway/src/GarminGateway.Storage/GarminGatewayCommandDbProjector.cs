using DigiTFactory.Libraries.SeedWorks.Events;
using GarminGateway.Domain;
using GarminGateway.DomainServices;
using Microsoft.Extensions.Logging;

namespace GarminGateway.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ GarminGatewayProjectionHandler.
/// </summary>
public class GarminGatewayCommandDbProjector : IDomainEventHandler<IGarminGateway>, IObserver<IDomainEvent<IGarminGateway>>
{
    private readonly IRepository _repository;
    private readonly ILogger<GarminGatewayCommandDbProjector> _logger;

    public GarminGatewayCommandDbProjector(IRepository repository, ILogger<GarminGatewayCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IGarminGateway> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
