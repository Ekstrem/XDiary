namespace Journal.Storage;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Implementation;

/// <summary>
/// Провайдер агрегата (канон §11): стримит события к агрегату — GetAggregateAsync
/// восстанавливает AnemicModel из event store (RecordEventReplay) и оборачивает в Aggregate.
/// </summary>
public sealed class RecordAggregateProvider : IAggregateProvider<IJournal, Aggregate>
{
    private readonly RecordEventReplay _replay;

    public RecordAggregateProvider(RecordEventReplay replay) => _replay = replay;

    public async Task<Aggregate> GetAggregateAsync(Guid id, CancellationToken ct = default)
    {
        var model = await _replay.ReplayAsync(id, ct) ?? new AnemicModel { Id = id };
        return Aggregate.CreateInstance(model);
    }

    public async Task<Aggregate> GetAggregateAsync(Guid id, long version, CancellationToken ct = default)
    {
        var aggregate = await GetAggregateAsync(id, ct);
        if (aggregate.Version != version)
            throw new InvalidOperationException($"Record {id} has version {aggregate.Version}, requested {version}.");
        return aggregate;
    }

    public Aggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public Aggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
