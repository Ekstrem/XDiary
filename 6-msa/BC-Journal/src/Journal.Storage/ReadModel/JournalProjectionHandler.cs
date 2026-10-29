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

        if (commandName == nameof(IRecordAggregate.DeleteRecord))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= domainEvent.Command.Version)
            return;

        var readModel = existing ?? new JournalReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

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

        if (entry.CommandName == nameof(IRecordAggregate.DeleteRecord))
        {
            await _store.DeleteAsync(aggregateId, ct);
            return;
        }

        var existing = await TryGetAsync(aggregateId, ct);
        if (existing != null && existing.Version >= entry.Version)
            return;

        var readModel = existing ?? new JournalReadModel { Id = aggregateId, CreatedAt = DateTimeOffset.UtcNow };

        ApplyValueObjects(readModel, DomainEventJson.Deserialize(entry.ChangedValueObjectsJson));

        readModel.Version = entry.Version;
        readModel.UpdatedAt = DateTimeOffset.UtcNow;

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
            if (r.Get<string>("StartedAt") is { } startedAt)
                model.StartedAt = startedAt;
            model.DurationMin = r.Get<int>("DurationMin");
        }

        if (vos.TryGetValue("Partner", out var partner) && partner is DynamicValueObject p)
            model.Partner = p.Get<string>("Name") ?? string.Empty;

        if (vos.TryGetValue("Place", out var place) && place is DynamicValueObject pl)
            model.Place = pl.Get<string>("Name") ?? string.Empty;

        if (vos.TryGetValue("Positions", out var positions) && positions is DynamicValueObject po)
            model.Positions = ToList(po.Get<string[]>("Items"));
        if (vos.TryGetValue("Tags", out var tags) && tags is DynamicValueObject tg)
            model.Tags = ToList(tg.Get<string[]>("Items"));
        if (vos.TryGetValue("Costumes", out var costumes) && costumes is DynamicValueObject cs)
            model.Costumes = ToList(cs.Get<string[]>("Items"));
        if (vos.TryGetValue("Toys", out var toys) && toys is DynamicValueObject ty)
            model.Toys = ToList(ty.Get<string[]>("Items"));
        if (vos.TryGetValue("Fantasies", out var fantasies) && fantasies is DynamicValueObject fa)
            model.Fantasies = ToList(fa.Get<string[]>("Items"));

        if (vos.TryGetValue("Contraception", out var contraception) && contraception is DynamicValueObject cp)
        {
            model.Contraceptives = ToList(cp.Get<string[]>("Contraceptives"));
            model.ContraceptiveCount = cp.Get<int>("ContraceptiveCount");
        }

        if (vos.TryGetValue("Orgasms", out var orgasms) && orgasms is DynamicValueObject og)
        {
            model.OrgasmsMe = og.Get<int>("OrgasmsMe");
            model.OrgasmsPartner = og.Get<int>("OrgasmsPartner");
            model.OrgasmMeType = ToList(og.Get<string[]>("OrgasmMeType"));
            model.OrgasmPartnerType = ToList(og.Get<string[]>("OrgasmPartnerType"));
            model.OrgasmMeRating = og.Get<int>("OrgasmMeRating");
            model.OrgasmPartnerRating = og.Get<int>("OrgasmPartnerRating");
        }

        if (vos.TryGetValue("Origin", out var origin) && origin is DynamicValueObject or)
        {
            model.Garmin = or.Get<bool>("Garmin");
            model.GarminId = or.Get<string>("GarminId") ?? string.Empty;
        }
    }

    private static List<string> ToList(string[]? items)
        => items?.ToList() ?? new List<string>();
}
