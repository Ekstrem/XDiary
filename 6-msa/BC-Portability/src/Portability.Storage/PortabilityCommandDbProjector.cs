using DigiTFactory.Libraries.SeedWorks.Events;
using Portability.Domain;
using Portability.DomainServices;
using Microsoft.Extensions.Logging;

namespace Portability.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ PortabilityProjectionHandler.
/// </summary>
public class PortabilityCommandDbProjector : IDomainEventHandler<IPortability>, IObserver<IDomainEvent<IPortability>>
{
    private readonly IRepository _repository;
    private readonly ILogger<PortabilityCommandDbProjector> _logger;

    public PortabilityCommandDbProjector(IRepository repository, ILogger<PortabilityCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IPortability> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
