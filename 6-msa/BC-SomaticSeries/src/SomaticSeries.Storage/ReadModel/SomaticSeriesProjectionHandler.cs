using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using SomaticSeries.Domain;
using SomaticSeries.InternalContracts;
using Microsoft.Extensions.Logging;

namespace SomaticSeries.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → SomaticSeriesReadModel → Read Store.
/// Append-only: только INSERT/UPDATE (upsert по Id); удаление не предусмотрено.
/// </summary>
public class SomaticSeriesProjectionHandler : IDomainEventHandler<ISomaticSeries>, IObserver<IDomainEvent<ISomaticSeries>>
{
    private readonly IReadModelStore<ISomaticSeries, SomaticSeriesReadModel> _store;
    private readonly IReadRepository<ISomaticSeries, SomaticSeriesReadModel> _reader;
    private readonly ILogger<SomaticSeriesProjectionHandler> _logger;

    public SomaticSeriesProjectionHandler(
        IReadModelStore<ISomaticSeries, SomaticSeriesReadModel> store,
        IReadRepository<ISomaticSeries, SomaticSeriesReadModel> reader,
        ILogger<SomaticSeriesProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций SomaticSeriesReadModel.");

    public void OnNext(IDomainEvent<ISomaticSeries> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<ISomaticSeries> domainEvent, CancellationToken ct)
    {
        var aggregateId = domainEvent.Id;

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new SomaticSeriesReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, domainEvent.ChangedValueObjects);

        readModel.Version = domainEvent.Command.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);

        _logger.LogInformation(
            "Проекция обновлена: Id={AggregateId}, Command={Command}, Version={Version}",
            aggregateId, domainEvent.Command.CommandName, domainEvent.Command.Version);
    }

    /// <summary>Проекция из записи Event Store (rebuild).</summary>
    public async Task ProjectFromEntryAsync(DomainEventEventEntry entry, CancellationToken ct)
    {
        var aggregateId = entry.Id;

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new SomaticSeriesReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<SomaticSeriesReadModel?> TryGetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            return await _reader.GetByIdAsync(id, ct);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyValueObjects(SomaticSeriesReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.T = r.Get<string>("T") ?? string.Empty;

        if (vos.TryGetValue("HeartRate", out var hr) && hr is DynamicValueObject h)
            model.Hr = h.Get<int>("Hr");

        if (vos.TryGetValue("BodyBattery", out var bb) && bb is DynamicValueObject b)
            model.BodyBattery = b.Get<int>("BodyBattery");
    }
}
