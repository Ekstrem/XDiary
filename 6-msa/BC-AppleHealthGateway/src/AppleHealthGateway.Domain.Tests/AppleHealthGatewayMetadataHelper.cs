using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Metadata;

namespace AppleHealthGateway.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataAppleImportAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ AppleHealthGatewayMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class AppleHealthGatewayMetadataHelper
{
    public static IAppleImportAggregate CreateAggregate(IAnemicModel<IAppleHealthGateway> currentModel)
    {
        var factory = new AbstractAggregateFactory<IAppleHealthGateway>(new InMemoryAppleHealthGatewayMetadataCache());
        var metadata = factory.GetMetadataAsync("AppleImport", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IAppleHealthGateway>.CreateInstance(currentModel, scope);
        return MetadataAppleImportAggregate.Create(aggregate, currentModel, scope);
    }

    public static IAppleImportAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IAppleHealthGateway>.CreateDefault(id));

    internal sealed class InMemoryAppleHealthGatewayMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = AppleHealthGatewayMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
