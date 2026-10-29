using DigiTFactory.Libraries.SeedWorks.Events;
using AppleHealthGateway.Domain;
using AppleHealthGateway.DomainServices;
using Microsoft.Extensions.Logging;

namespace AppleHealthGateway.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ AppleHealthGatewayProjectionHandler.
/// </summary>
public class AppleHealthGatewayCommandDbProjector : IDomainEventHandler<IAppleHealthGateway>, IObserver<IDomainEvent<IAppleHealthGateway>>
{
    private readonly IRepository _repository;
    private readonly ILogger<AppleHealthGatewayCommandDbProjector> _logger;

    public AppleHealthGatewayCommandDbProjector(IRepository repository, ILogger<AppleHealthGatewayCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IAppleHealthGateway> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
