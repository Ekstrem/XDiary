using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.Domain.Metadata;

namespace DailyMetrics.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataDailyMetricsAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ DailyMetricsMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class DailyMetricsMetadataHelper
{
    public static IDailyMetricsAggregate CreateAggregate(IAnemicModel<IDailyMetrics> currentModel)
    {
        var factory = new AbstractAggregateFactory<IDailyMetrics>(new InMemoryDailyMetricsMetadataCache());
        var metadata = factory.GetMetadataAsync("DailyMetrics", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IDailyMetrics>.CreateInstance(currentModel, scope);
        return MetadataDailyMetricsAggregate.Create(aggregate, currentModel, scope);
    }

    public static IDailyMetricsAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IDailyMetrics>.CreateDefault(id));

    internal sealed class InMemoryDailyMetricsMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = DailyMetricsMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
