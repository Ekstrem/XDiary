using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.Domain.Metadata;

namespace Partners.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataPartnerAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ PartnersMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class PartnersMetadataHelper
{
    public static IPartnerAggregate CreateAggregate(IAnemicModel<IPartners> currentModel)
    {
        var factory = new AbstractAggregateFactory<IPartners>(new InMemoryPartnersMetadataCache());
        var metadata = factory.GetMetadataAsync("Partner", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IPartners>.CreateInstance(currentModel, scope);
        return MetadataPartnerAggregate.Create(aggregate, currentModel, scope);
    }

    public static IPartnerAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IPartners>.CreateDefault(id));

    internal sealed class InMemoryPartnersMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = PartnersMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
