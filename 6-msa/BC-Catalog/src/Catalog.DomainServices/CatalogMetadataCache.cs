using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using Catalog.Domain.Metadata;

namespace Catalog.DomainServices;

/// <summary>
/// In-memory РєСЌС€ РјРµС‚Р°РґР°РЅРЅС‹С… Р°РіСЂРµРіР°С‚Р° CatalogItem.
/// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєР°РЅРѕРЅРёС‡РµСЃРєРёРµ РјРµС‚Р°РґР°РЅРЅС‹Рµ РёР· CatalogMetadata (РЅРµ РёР· Р‘Р”) вЂ”
/// РґР»СЏ РїСЂРѕС„РёР»СЏ Single РјРµС‚Р°РґР°РЅРЅС‹Рµ Р·Р°РґР°РЅС‹ РєРѕРґРѕРј, Р° РЅРµ РІ abstract_aggregate.
/// </summary>
public sealed class CatalogMetadataCache : IMetadataCache
{
    private readonly AggregateMetadata _metadata = CatalogMetadata.Build();

    public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
        => Task.FromResult(_metadata);

    public void Invalidate(string aggregateName) { }
    public void InvalidateAll() { }
}
