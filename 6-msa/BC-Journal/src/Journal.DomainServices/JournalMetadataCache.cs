using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using Journal.Domain.Metadata;

namespace Journal.DomainServices;

/// <summary>
/// In-memory кэш метаданных агрегата Record.
/// Возвращает канонические метаданные из JournalMetadata (не из БД) —
/// для профиля Single метаданные заданы кодом, а не в abstract_aggregate.
/// </summary>
public sealed class JournalMetadataCache : IMetadataCache
{
    private readonly AggregateMetadata _metadata = JournalMetadata.Build();

    public Task<AggregateMetadata> GetOrLoadAsync(string aggregateName, CancellationToken ct = default)
        => Task.FromResult(_metadata);

    public void Invalidate(string aggregateName) { }
    public void InvalidateAll() { }
}
