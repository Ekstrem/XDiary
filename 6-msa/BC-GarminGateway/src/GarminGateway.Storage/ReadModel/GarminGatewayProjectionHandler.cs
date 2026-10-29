using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using GarminGateway.Domain;
using GarminGateway.InternalContracts;
using Microsoft.Extensions.Logging;

namespace GarminGateway.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → GarminSyncReadModel → Read Store.
/// Append-only: только INSERT/UPDATE (upsert по Id).
/// </summary>
public class GarminGatewayProjectionHandler : IDomainEventHandler<IGarminGateway>, IObserver<IDomainEvent<IGarminGateway>>
{
    private readonly IReadModelStore<IGarminGateway, GarminSyncReadModel> _store;
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _reader;
    private readonly ILogger<GarminGatewayProjectionHandler> _logger;

    public GarminGatewayProjectionHandler(
        IReadModelStore<IGarminGateway, GarminSyncReadModel> store,
        IReadRepository<IGarminGateway, GarminSyncReadModel> reader,
        ILogger<GarminGatewayProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций GarminSyncReadModel.");

    public void OnNext(IDomainEvent<IGarminGateway> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IGarminGateway> domainEvent, CancellationToken ct)
    {
        var aggregateId = domainEvent.Id;

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new GarminSyncReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        var readModel = existing ?? new GarminSyncReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<GarminSyncReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(GarminSyncReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.SourceId = r.Get<string>("SourceId") ?? string.Empty;

        if (vos.TryGetValue("Credentials", out var creds) && creds is DynamicValueObject c)
        {
            model.ApiKey = c.Get<string>("ApiKey") ?? string.Empty;
            model.OauthToken = c.Get<string>("OauthToken") ?? string.Empty;
            model.TokenExpiry = c.Get<string>("TokenExpiry") ?? string.Empty;
        }

        if (vos.TryGetValue("SyncConfig", out var cfg) && cfg is DynamicValueObject s)
        {
            model.Metrics = ToList(s.Get<string[]>("Metrics"));
            model.WindowDays = s.Get<int>("WindowDays");
        }

        if (vos.TryGetValue("LastSync", out var ls) && ls is DynamicValueObject l)
            model.LastSync = l.Get<string>("Timestamp") ?? string.Empty;
    }

    private static List<string> ToList(string[]? items)
        => items?.ToList() ?? new List<string>();
}
