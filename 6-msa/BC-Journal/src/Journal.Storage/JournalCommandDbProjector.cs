using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.DomainServices;
using Microsoft.Extensions.Logging;

namespace Journal.Storage;

/// <summary>
/// РџСЂРѕРµРєС‚РѕСЂ РІ Command Store: РїРѕРґРїРёСЃС‹РІР°РµС‚СЃСЏ РЅР° С€РёРЅСѓ Рё СЃРѕС…СЂР°РЅСЏРµС‚ РґРѕРјРµРЅРЅС‹Рµ СЃРѕР±С‹С‚РёСЏ
/// (Event Sourcing). Р Р°Р±РѕС‚Р°РµС‚ РїР°СЂР°Р»Р»РµР»СЊРЅРѕ СЃ JournalProjectionHandler.
/// </summary>
public class JournalCommandDbProjector : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IRepository _repository;
    private readonly ILogger<JournalCommandDbProjector> _logger;

    public JournalCommandDbProjector(IRepository repository, ILogger<JournalCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.");

    public void OnNext(IDomainEvent<IJournal> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
