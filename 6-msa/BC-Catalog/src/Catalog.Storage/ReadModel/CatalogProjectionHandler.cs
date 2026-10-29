using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.InternalContracts;
using Microsoft.Extensions.Logging;

namespace Catalog.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → CatalogReadModel → Read Store.
/// Идемпотентен: пропускает событие, если Version уже обработана.
/// </summary>
public class CatalogProjectionHandler : IDomainEventHandler<ICatalog>, IObserver<IDomainEvent<ICatalog>>
{
    private readonly IReadModelStore<ICatalog, CatalogReadModel> _store;
    private readonly IReadRepository<ICatalog, CatalogReadModel> _reader;
    private readonly ILogger<CatalogProjectionHandler> _logger;

    public CatalogProjectionHandler(
        IReadModelStore<ICatalog, CatalogReadModel> store,
        IReadRepository<ICatalog, CatalogReadModel> reader,
        ILogger<CatalogProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций CatalogReadModel.");

    public void OnNext(IDomainEvent<ICatalog> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<ICatalog> domainEvent, CancellationToken ct)
    {
        var commandName = domainEvent.Command.CommandName;
        var aggregateId = domainEvent.Id;

        if (commandName == nameof(ICatalogItemAggregate.DeleteItem))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new CatalogReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, domainEvent.ChangedValueObjects);

        readModel.Version = domainEvent.Command.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);

        _logger.LogInformation(
            "Проекция обновлена: Id={AggregateId}, Command={Command}, Version={Version}",
            aggregateId, commandName, domainEvent.Command.Version);
    }

    /// <summary>Проекция из записи Event Store (rebuild).</summary>
    public async Task ProjectFromEntryAsync(DomainEventEventEntry entry, CancellationToken ct)
    {
        var aggregateId = entry.Id;

        if (entry.CommandName == nameof(ICatalogItemAggregate.DeleteItem))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new CatalogReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<CatalogReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(CatalogReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
        {
            model.Kind = r.Get<string>("Kind") ?? string.Empty;
            model.Name = r.Get<string>("Name") ?? string.Empty;
        }

        if (vos.TryGetValue("Photo", out var photo) && photo is DynamicValueObject ph)
            model.Photo = ph.Get<string>("DataUrl") ?? string.Empty;

        if (vos.TryGetValue("Notes", out var notes) && notes is DynamicValueObject n)
            model.Notes = n.Get<string>("Text") ?? string.Empty;

        if (vos.TryGetValue("Brand", out var brand) && brand is DynamicValueObject b)
            model.Brand = b.Get<string>("Value") ?? string.Empty;

        if (vos.TryGetValue("Archive", out var archive) && archive is DynamicValueObject a)
            model.Archived = a.Get<bool>("Archived");

        if (vos.TryGetValue("FantasyPartner", out var fp) && fp is DynamicValueObject f)
            model.Partner = f.Get<string>("Partner") ?? string.Empty;
    }
}
