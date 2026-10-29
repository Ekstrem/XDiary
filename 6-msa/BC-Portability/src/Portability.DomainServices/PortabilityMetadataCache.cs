using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using Portability.Domain.Metadata;

namespace Portability.DomainServices;

/// <summary>
/// In-memory РєСЌС€ РјРµС‚Р°РґР°РЅРЅС‹С… Р°РіСЂРµРіР°С‚Р° Portability.
/// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєР°РЅРѕРЅРёС‡РµСЃРєРёРµ РјРµС‚Р°РґР°РЅРЅС‹Рµ РёР· PortabilityMetadata (РЅРµ РёР· Р‘Р”) вЂ”
/// РґР»СЏ РїСЂРѕС„РёР»СЏ Single РјРµС‚Р°РґР°РЅРЅС‹Рµ Р·Р°РґР°РЅС‹ РєРѕРґРѕРј, Р° РЅРµ РІ abstract_aggregate.
/// </summary>
public sealed class PortabilityMetadataCache : IMetadataCache
{
    private readonly AggregateMetadata _metadata = PortabilityMetadata.Build();

    public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
        => Task.FromResult(_metadata);

    public void Invalidate(string aggregateName) { }
    public void InvalidateAll() { }
}
