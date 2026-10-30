using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.Domain.Metadata;

namespace Journal.Domain.Tests;

/// <summary>
/// РЎС‚СЂРѕРёС‚ MetadataRecordAggregate РёР· in-memory РјРµС‚Р°РґР°РЅРЅС‹С… (Р±РµР· Р‘Р”).
/// РСЃРїРѕР»СЊР·СѓРµС‚ С‚РѕС‚ Р¶Рµ JournalMetadata.Build(), С‡С‚Рѕ Рё СЂР°РЅС‚Р°Р№Рј.
/// </summary>
public static class JournalMetadataHelper
{
    public static IRecordAggregate CreateAggregate(IAnemicModel<IJournal> currentModel)
    {
        var factory = new AbstractAggregateFactory<IJournal>(new InMemoryJournalMetadataCache());
        var metadata = factory.GetMetadataAsync("Record", CancellationToken.None).GetAwaiter().GetResult();
        var scope = factory.BuildScope(metadata, currentModel);
        var aggregate = Aggregate<IJournal>.CreateInstance(currentModel, scope);
        return MetadataRecordAggregate.Create(aggregate, currentModel, scope);
    }

    public static IRecordAggregate CreateNewAggregate(Guid id)
        => CreateAggregate(DynamicAnemicModel<IJournal>.CreateDefault(id));

    internal sealed class InMemoryJournalMetadataCache : IMetadataCache
    {
        private readonly AggregateMetadata _metadata = JournalMetadata.Build();

        public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
            => Task.FromResult(_metadata);

        public void Invalidate(string aggregateName) { }
        public void InvalidateAll() { }
    }
}
