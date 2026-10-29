using DigiTFactory.Libraries.SeedWorks.Events;
using Partners.Domain;
using Partners.DomainServices;
using Microsoft.Extensions.Logging;

namespace Partners.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ PartnersProjectionHandler.
/// </summary>
public class PartnersCommandDbProjector : IDomainEventHandler<IPartners>, IObserver<IDomainEvent<IPartners>>
{
    private readonly IRepository _repository;
    private readonly ILogger<PartnersCommandDbProjector> _logger;

    public PartnersCommandDbProjector(IRepository repository, ILogger<PartnersCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IPartners> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
