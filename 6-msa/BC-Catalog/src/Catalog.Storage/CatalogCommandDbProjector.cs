using DigiTFactory.Libraries.SeedWorks.Events;
using Catalog.Domain;
using Catalog.DomainServices;
using Microsoft.Extensions.Logging;

namespace Catalog.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ CatalogProjectionHandler.
/// </summary>
public class CatalogCommandDbProjector : IDomainEventHandler<ICatalog>, IObserver<IDomainEvent<ICatalog>>
{
    private readonly IRepository _repository;
    private readonly ILogger<CatalogCommandDbProjector> _logger;

    public CatalogCommandDbProjector(IRepository repository, ILogger<CatalogCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<ICatalog> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
