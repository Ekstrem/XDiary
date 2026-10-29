using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.InternalContracts;
using Microsoft.Extensions.Logging;

namespace Privacy.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → PrivacyReadModel → Read Store.
/// Идемпотентен: пропускает событие, если Version уже обработана.
/// </summary>
public class PrivacyProjectionHandler : IDomainEventHandler<IPrivacy>, IObserver<IDomainEvent<IPrivacy>>
{
    private readonly IReadModelStore<IPrivacy, PrivacyReadModel> _store;
    private readonly IReadRepository<IPrivacy, PrivacyReadModel> _reader;
    private readonly ILogger<PrivacyProjectionHandler> _logger;

    public PrivacyProjectionHandler(
        IReadModelStore<IPrivacy, PrivacyReadModel> store,
        IReadRepository<IPrivacy, PrivacyReadModel> reader,
        ILogger<PrivacyProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций PrivacyReadModel.");

    public void OnNext(IDomainEvent<IPrivacy> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IPrivacy> domainEvent, CancellationToken ct)
    {
        var commandName = domainEvent.Command.CommandName;
        var aggregateId = domainEvent.Id;

        if (commandName == nameof(IPinCredentialAggregate.RemovePin))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new PrivacyReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        if (entry.CommandName == nameof(IPinCredentialAggregate.RemovePin))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new PrivacyReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<PrivacyReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(PrivacyReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            model.UserId = r.Get<string>("UserId") ?? string.Empty;

        if (vos.TryGetValue("Salt", out var salt) && salt is DynamicValueObject s)
            model.Salt = s.Get<string>("Salt") ?? string.Empty;

        if (vos.TryGetValue("Hash", out var hash) && hash is DynamicValueObject h)
            model.Hash = h.Get<string>("Hash") ?? string.Empty;

        if (vos.TryGetValue("Iterations", out var it) && it is DynamicValueObject i)
            model.Iterations = i.Get<int>("Iterations");
    }
}
