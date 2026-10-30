using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.InternalContracts;
using Microsoft.Extensions.Logging;

namespace Journal.Storage.ReadModel;

/// <summary>
/// Проекционный обработчик: доменное событие → JournalReadModel → Read Store.
/// Идемпотентен: пропускает событие, если Version уже обработана.
/// </summary>
public class JournalProjectionHandler : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IReadModelStore<IJournal, JournalReadModel> _store;
    private readonly IReadRepository<IJournal, JournalReadModel> _reader;
    private readonly ILogger<JournalProjectionHandler> _logger;

    public JournalProjectionHandler(
        IReadModelStore<IJournal, JournalReadModel> store,
        IReadRepository<IJournal, JournalReadModel> reader,
        ILogger<JournalProjectionHandler> logger)
    {
        _store = store;
        _reader = reader;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) => _logger.LogError(error, "Ошибка в потоке проекций JournalReadModel.");

    public void OnNext(IDomainEvent<IJournal> domainEvent)
        => ProjectAsync(domainEvent, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ProjectAsync(IDomainEvent<IJournal> domainEvent, CancellationToken ct)
    {
        var commandName = domainEvent.Command.CommandName;
        var aggregateId = domainEvent.Id;

        if (commandName == nameof(IRecordAggregate.Delete))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new JournalReadModel { Id = aggregateId };

        ApplyValueObjects(readModel, domainEvent.ChangedValueObjects);

        readModel.Version = domainEvent.Command.Version;
        readModel.RecordUpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);

        _logger.LogInformation(
            "Проекция обновлена: Id={AggregateId}, Command={Command}, Version={Version}",
            aggregateId, commandName, domainEvent.Command.Version);
    }

    /// <summary>Проекция из записи Event Store (rebuild).</summary>
    public async Task ProjectFromEntryAsync(DomainEventEventEntry entry, CancellationToken ct)
    {
        var aggregateId = entry.Id;

        if (entry.CommandName == nameof(IRecordAggregate.Delete))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new JournalReadModel { Id = aggregateId };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.RecordUpdatedAt = DateTimeOffset.UtcNow;

        await _store.UpsertAsync(readModel, ct);
    }

    private async Task<JournalReadModel?> TryGetAsync(Guid id, CancellationToken ct)
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

    private static void ApplyValueObjects(JournalReadModel model, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
        {
            model.PartnerId = r.Get<string>("PartnerId") ?? string.Empty;
            model.StartedAt = r.Get<string>("StartedAt") ?? string.Empty;
        }

        if (vos.TryGetValue("Duration", out var d) && d is DynamicValueObject dur)
            model.DurationMin = dur.Get<int>("DurationMin");

        if (vos.TryGetValue("Place", out var p) && p is DynamicValueObject place)
            model.Place = place.Get<string>("Place") ?? string.Empty;

        if (vos.TryGetValue("Positions", out var pos) && pos is DynamicValueObject positions)
            model.Positions = ToList(positions.Get<string[]>("Positions"));

        if (vos.TryGetValue("Accessories", out var acc) && acc is DynamicValueObject accessories)
        {
            model.Costumes = ToList(accessories.Get<string[]>("Costumes"));
            model.Toys = ToList(accessories.Get<string[]>("Toys"));
            model.Fantasies = ToList(accessories.Get<string[]>("Fantasies"));
            model.Tags = ToList(accessories.Get<string[]>("Tags"));
        }

        if (vos.TryGetValue("Contraception", out var c) && c is DynamicValueObject contraception)
        {
            model.Contraceptives = ToList(contraception.Get<string[]>("Contraceptives"));
            model.ContraceptiveCount = contraception.Get<int>("ContraceptiveCount");
        }

        if (vos.TryGetValue("Orgasms", out var o) && o is DynamicValueObject orgasms)
        {
            model.OrgasmsMe = orgasms.Get<int>("OrgasmsMe");
            model.OrgasmsPartner = orgasms.Get<int>("OrgasmsPartner");
            model.OrgasmMeType = ToList(orgasms.Get<string[]>("OrgasmMeType"));
            model.OrgasmPartnerType = ToList(orgasms.Get<string[]>("OrgasmPartnerType"));
            model.OrgasmMeRating = orgasms.Get<int>("OrgasmMeRating");
            model.OrgasmPartnerRating = orgasms.Get<int>("OrgasmPartnerRating");
        }

        if (vos.TryGetValue("Origin", out var origin) && origin is DynamicValueObject org)
        {
            model.Garmin = org.Get<bool>("Garmin");
            model.GarminId = org.Get<string>("GarminId") ?? string.Empty;
            model.CreatedAt = org.Get<string>("CreatedAt") ?? string.Empty;
            model.UpdatedAt = org.Get<string>("UpdatedAt") ?? string.Empty;
        }
    }

    private static List<string> ToList(string[]? items)
        => items?.ToList() ?? new List<string>();
}
