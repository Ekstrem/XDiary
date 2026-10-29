using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using GarminGateway.Domain.Metadata;

namespace GarminGateway.DomainServices;

/// <summary>
/// In-memory РєСЌС€ РјРµС‚Р°РґР°РЅРЅС‹С… Р°РіСЂРµРіР°С‚Р° GarminGateway.
/// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєР°РЅРѕРЅРёС‡РµСЃРєРёРµ РјРµС‚Р°РґР°РЅРЅС‹Рµ РёР· GarminGatewayMetadata (РЅРµ РёР· Р‘Р”) вЂ”
/// РґР»СЏ РїСЂРѕС„РёР»СЏ Single РјРµС‚Р°РґР°РЅРЅС‹Рµ Р·Р°РґР°РЅС‹ РєРѕРґРѕРј, Р° РЅРµ РІ abstract_aggregate.
/// </summary>
public sealed class GarminGatewayMetadataCache : IMetadataCache
{
    private readonly AggregateMetadata _metadata = GarminGatewayMetadata.Build();

    public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
        => Task.FromResult(_metadata);

    public void Invalidate(string aggregateName) { }
    public void InvalidateAll() { }
}
