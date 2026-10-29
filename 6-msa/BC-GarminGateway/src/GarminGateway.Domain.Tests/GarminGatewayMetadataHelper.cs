using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Metadata;

namespace GarminGateway.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataGarminSyncAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ GarminGatewayMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class GarminGatewayMetadataHelper
{
    public static IGarminSyncAggregate CreateAggregate(IAnemicModel<IGarminGateway> currentModel)
    {
        var factory = new AbstractAggregateFactory<IGarminGateway>(new InMemoryGarminGatewayMetadataCache());
        var metadata = factory.GetMetadataAsync("GarminSync", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IGarminGateway>.CreateInstance(currentModel, scope);
        return MetadataGarminSyncAggregate.Create(aggregate, currentModel, scope);
    }

    public static IGarminSyncAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IGarminGateway>.CreateDefault(id));

    internal sealed class InMemoryGarminGatewayMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = GarminGatewayMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
