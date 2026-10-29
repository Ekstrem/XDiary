using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.Domain.Metadata;

namespace SomaticSeries.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataSomaticPointAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ SomaticSeriesMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class SomaticSeriesMetadataHelper
{
    public static ISomaticPointAggregate CreateAggregate(IAnemicModel<ISomaticSeries> currentModel)
    {
        var factory = new AbstractAggregateFactory<ISomaticSeries>(new InMemorySomaticSeriesMetadataCache());
        var metadata = factory.GetMetadataAsync("SomaticPoint", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<ISomaticSeries>.CreateInstance(currentModel, scope);
        return MetadataSomaticPointAggregate.Create(aggregate, currentModel, scope);
    }

    public static ISomaticPointAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<ISomaticSeries>.CreateDefault(id));

    internal sealed class InMemorySomaticSeriesMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = SomaticSeriesMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
