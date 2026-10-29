using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.Domain.Metadata;

namespace Catalog.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataCatalogItemAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ CatalogMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class CatalogMetadataHelper
{
    public static ICatalogItemAggregate CreateAggregate(IAnemicModel<ICatalog> currentModel)
    {
        var factory = new AbstractAggregateFactory<ICatalog>(new InMemoryCatalogMetadataCache());
        var metadata = factory.GetMetadataAsync("CatalogItem", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<ICatalog>.CreateInstance(currentModel, scope);
        return MetadataCatalogItemAggregate.Create(aggregate, currentModel, scope);
    }

    public static ICatalogItemAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<ICatalog>.CreateDefault(id));

    internal sealed class InMemoryCatalogMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = CatalogMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
