using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Portability.Domain.Metadata;

namespace Portability.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataCsvDocumentAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ PortabilityMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class PortabilityMetadataHelper
{
    public static ICsvDocumentAggregate CreateAggregate(IAnemicModel<IPortability> currentModel)
    {
        var factory = new AbstractAggregateFactory<IPortability>(new InMemoryPortabilityMetadataCache());
        var metadata = factory.GetMetadataAsync("CsvDocument", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IPortability>.CreateInstance(currentModel, scope);
        return MetadataCsvDocumentAggregate.Create(aggregate, currentModel, scope);
    }

    public static ICsvDocumentAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IPortability>.CreateDefault(id));

    internal sealed class InMemoryPortabilityMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = PortabilityMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
