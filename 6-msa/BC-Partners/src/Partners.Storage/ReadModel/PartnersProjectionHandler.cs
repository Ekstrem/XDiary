using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.InternalContracts;
using Microsoft.Extensions.Logging;

namespace Partners.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → PartnersReadModel → Read Store.
/// Идемпотентен: пропускает событие, если Version уже обработана.
/// </summary>
public class PartnersProjectionHandler : IDomainEventHandler<IPartners>, IObserver<IDomainEvent<IPartners>>
{
    private readonly IReadModelStore<IPartners, PartnersReadModel> _store;
    private readonly IReadRepository<IPartners, PartnersReadModel> _reader;
    private readonly ILogger<PartnersProjectionHandler> _logger;

    public PartnersProjectionHandler(
        IReadModelStore<IPartners, PartnersReadModel> store,
        IReadRepository<IPartners, PartnersReadModel> reader,
        ILogger<PartnersProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций PartnersReadModel.");

    public void OnNext(IDomainEvent<IPartners> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IPartners> domainEvent, CancellationToken ct)
    {
        var commandName = domainEvent.Command.CommandName;
        var aggregateId = domainEvent.Id;

        if (commandName == nameof(IPartnerAggregate.DeletePartner))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new PartnersReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        if (entry.CommandName == nameof(IPartnerAggregate.DeletePartner))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new PartnersReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<PartnersReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(PartnersReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.Name = r.Get<string>("Name") ?? string.Empty;

        if (vos.TryGetValue("Photo", out var photo) && photo is DynamicValueObject ph)
            model.Photo = ph.Get<string>("DataUrl") ?? string.Empty;

        if (vos.TryGetValue("Gender", out var gender) && gender is DynamicValueObject g)
            model.Gender = g.Get<string>("Value") ?? string.Empty;

        if (vos.TryGetValue("Notes", out var notes) && notes is DynamicValueObject n)
            model.Notes = n.Get<string>("Text") ?? string.Empty;
    }
}
