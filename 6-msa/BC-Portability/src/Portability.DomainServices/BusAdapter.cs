using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Microsoft.Extensions.Logging;

namespace Portability.DomainServices;

/// <summary>
/// РќР°Р±Р»СЋРґР°С‚РµР»СЊ СЂРµР·СѓР»СЊС‚Р°С‚Р° Р°РіСЂРµРіР°С‚Р° вЂ” РїСѓР±Р»РёРєСѓРµС‚ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ С€РёРЅСѓ.
/// </summary>
internal class BusAdapter : IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<BusAdapter> _logger;

    public BusAdapter(IEventBus eventBus, ILogger<BusAdapter> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public void OnCompleted() => _logger.LogInformation("РџРѕС‚РѕРє РґРѕРјРµРЅРЅС‹С… СЃРѕР±С‹С‚РёР№ Р¶СѓСЂРЅР°Р»Р° Р·Р°РІРµСЂС€С‘РЅ.");

    public void OnError(Exception error) => _logger.LogError(error, "РћС€РёР±РєР° РІ РїРѕС‚РѕРєРµ РґРѕРјРµРЅРЅС‹С… СЃРѕР±С‹С‚РёР№ Р¶СѓСЂРЅР°Р»Р°.");

    public void OnNext(AggregateResult<IPortability, IAnemicModel<IPortability>> value)
    {
        _logger.LogInformation(
            "DE! Command={Command}; Version={Version}; Result={Result}",
            value.Event.Command.CommandName, value.Event.Version, value.Event.Result);

        _eventBus.Publish(value.Event);
    }
}
