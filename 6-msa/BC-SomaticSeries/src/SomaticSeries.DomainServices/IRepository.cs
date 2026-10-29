using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;

namespace SomaticSeries.DomainServices;

/// <summary>
/// Р РµРїРѕР·РёС‚РѕСЂРёР№ Р°РЅРµРјРёС‡РЅС‹С… РјРѕРґРµР»РµР№ Р°РіСЂРµРіР°С‚Р° SomaticPoint.
/// </summary>
public interface IRepository : IAnemicModelRepository<ISomaticSeries, IAnemicModel<ISomaticSeries>>
{
    /// <summary>
    /// РЎРѕС…СЂР°РЅРёС‚СЊ РґРѕРјРµРЅРЅРѕРµ СЃРѕР±С‹С‚РёРµ РІ Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<ISomaticSeries> domainEvent, CancellationToken cancellationToken = default);
}
