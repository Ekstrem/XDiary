using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Portability.Domain;
using Portability.InternalContracts;
using Microsoft.Extensions.Logging;

namespace Portability.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → CsvDocumentReadModel → Read Store.
/// Append-only: только INSERT/UPDATE (upsert по Id).
/// </summary>
public class PortabilityProjectionHandler : IDomainEventHandler<IPortability>, IObserver<IDomainEvent<IPortability>>
{
    private readonly IReadModelStore<IPortability, CsvDocumentReadModel> _store;
    private readonly IReadRepository<IPortability, CsvDocumentReadModel> _reader;
    private readonly ILogger<PortabilityProjectionHandler> _logger;

    public PortabilityProjectionHandler(
        IReadModelStore<IPortability, CsvDocumentReadModel> store,
        IReadRepository<IPortability, CsvDocumentReadModel> reader,
        ILogger<PortabilityProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций CsvDocumentReadModel.");

    public void OnNext(IDomainEvent<IPortability> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IPortability> domainEvent, CancellationToken ct)
    {
        var aggregateId = domainEvent.Id;

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new CsvDocumentReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        var readModel = existing ?? new CsvDocumentReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<CsvDocumentReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(CsvDocumentReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.SourceId = r.Get<string>("SourceId") ?? string.Empty;

        if (vos.TryGetValue("Headers", out var h) && h is DynamicValueObject headers)
            model.Headers = ToList(headers.Get<string[]>("Headers"));

        if (vos.TryGetValue("Rows", out var rowVo) && rowVo is DynamicValueObject rows)
            model.Rows = ToList(rows.Get<string[]>("Rows"));
    }

    private static List<string> ToList(string[]? items)
        => items?.ToList() ?? new List<string>();
}
