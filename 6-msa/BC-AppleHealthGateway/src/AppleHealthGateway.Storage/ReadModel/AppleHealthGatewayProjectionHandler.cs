using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using AppleHealthGateway.Domain;
using AppleHealthGateway.InternalContracts;
using Microsoft.Extensions.Logging;

namespace AppleHealthGateway.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → AppleImportReadModel → Read Store.
/// Append-only: только INSERT/UPDATE (upsert по Id).
/// </summary>
public class AppleHealthGatewayProjectionHandler : IDomainEventHandler<IAppleHealthGateway>, IObserver<IDomainEvent<IAppleHealthGateway>>
{
    private readonly IReadModelStore<IAppleHealthGateway, AppleImportReadModel> _store;
    private readonly IReadRepository<IAppleHealthGateway, AppleImportReadModel> _reader;
    private readonly ILogger<AppleHealthGatewayProjectionHandler> _logger;

    public AppleHealthGatewayProjectionHandler(
        IReadModelStore<IAppleHealthGateway, AppleImportReadModel> store,
        IReadRepository<IAppleHealthGateway, AppleImportReadModel> reader,
        ILogger<AppleHealthGatewayProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций AppleImportReadModel.");

    public void OnNext(IDomainEvent<IAppleHealthGateway> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IAppleHealthGateway> domainEvent, CancellationToken ct)
    {
        var aggregateId = domainEvent.Id;

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new AppleImportReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        var readModel = existing ?? new AppleImportReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<AppleImportReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(AppleImportReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.SourceId = r.Get<string>("SourceId") ?? string.Empty;

        if (vos.TryGetValue("ImportConfig", out var cfg) && cfg is DynamicValueObject c)
            model.Metrics = ToList(c.Get<string[]>("Metrics"));

        if (vos.TryGetValue("LastImport", out var li) && li is DynamicValueObject l)
            model.LastImport = l.Get<string>("Timestamp") ?? string.Empty;

        if (vos.TryGetValue("ProcessedFiles", out var pf) && pf is DynamicValueObject p)
            model.ProcessedFiles = ToList(p.Get<string[]>("Names"));
    }

    private static List<string> ToList(string[]? items)
        => items?.ToList() ?? new List<string>();
}
