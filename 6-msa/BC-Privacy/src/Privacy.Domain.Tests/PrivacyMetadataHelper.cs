using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.Domain.Metadata;

namespace Privacy.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataPinCredentialAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ PrivacyMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class PrivacyMetadataHelper
{
    public static IPinCredentialAggregate CreateAggregate(IAnemicModel<IPrivacy> currentModel)
    {
        var factory = new AbstractAggregateFactory<IPrivacy>(new InMemoryPrivacyMetadataCache());
        var metadata = factory.GetMetadataAsync("PinCredential", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IPrivacy>.CreateInstance(currentModel, scope);
        return MetadataPinCredentialAggregate.Create(aggregate, currentModel, scope);
    }

    public static IPinCredentialAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IPrivacy>.CreateDefault(id));

    internal sealed class InMemoryPrivacyMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = PrivacyMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
