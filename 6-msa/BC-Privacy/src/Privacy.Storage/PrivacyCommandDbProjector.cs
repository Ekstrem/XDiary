using DigiTFactory.Libraries.SeedWorks.Events;
using Privacy.Domain;
using Privacy.DomainServices;
using Microsoft.Extensions.Logging;

namespace Privacy.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ PrivacyProjectionHandler.
/// </summary>
public class PrivacyCommandDbProjector : IDomainEventHandler<IPrivacy>, IObserver<IDomainEvent<IPrivacy>>
{
    private readonly IRepository _repository;
    private readonly ILogger<PrivacyCommandDbProjector> _logger;

    public PrivacyCommandDbProjector(IRepository repository, ILogger<PrivacyCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IPrivacy> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
