using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.DomainServices;

/// <summary>
/// Р РµРїРѕР·РёС‚РѕСЂРёР№ Р°РЅРµРјРёС‡РЅС‹С… РјРѕРґРµР»РµР№ Р°РіСЂРµРіР°С‚Р° Record.
/// </summary>
public interface IRepository : IAnemicModelRepository<IJournal, IAnemicModel<IJournal>>
{
    /// <summary>
    /// РЎРѕС…СЂР°РЅРёС‚СЊ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<IJournal> domainEvent, CancellationToken cancellationToken = default);
}
