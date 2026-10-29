using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;

namespace GarminGateway.DomainServices;

/// <summary>
/// Р РµРїРѕР·РёС‚РѕСЂРёР№ Р°РЅРµРјРёС‡РЅС‹С… РјРѕРґРµР»РµР№ Р°РіСЂРµРіР°С‚Р° GarminGateway.
/// </summary>
public interface IRepository : IAnemicModelRepository<IGarminGateway, IAnemicModel<IGarminGateway>>
{
    /// <summary>
    /// РЎРѕС…СЂР°РЅРёС‚СЊ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<IGarminGateway> domainEvent, CancellationToken cancellationToken = default);
}
