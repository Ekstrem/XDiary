using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;

namespace AppleHealthGateway.DomainServices;

/// <summary>
/// Р РµРїРѕР·РёС‚РѕСЂРёР№ Р°РЅРµРјРёС‡РЅС‹С… РјРѕРґРµР»РµР№ Р°РіСЂРµРіР°С‚Р° AppleHealthGateway.
/// </summary>
public interface IRepository : IAnemicModelRepository<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>
{
    /// <summary>
    /// РЎРѕС…СЂР°РЅРёС‚СЊ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<IAppleHealthGateway> domainEvent, CancellationToken cancellationToken = default);
}
