using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;

namespace Privacy.DomainServices;

/// <summary>
/// Р РµРїРѕР·РёС‚РѕСЂРёР№ Р°РЅРµРјРёС‡РЅС‹С… РјРѕРґРµР»РµР№ Р°РіСЂРµРіР°С‚Р° PinCredential.
/// </summary>
public interface IRepository : IAnemicModelRepository<IPrivacy, IAnemicModel<IPrivacy>>
{
    /// <summary>
    /// РЎРѕС…СЂР°РЅРёС‚СЊ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<IPrivacy> domainEvent, CancellationToken cancellationToken = default);
}
