namespace Journal.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;

/// <summary>
/// Реальный replay из событий (канвас §10, FullEventSourcing): читает DomainEvents,
/// десериализует ChangedValueObjects и сворачивает их в AnemicModel.
/// Аналог FullEventSourcingRepository.GetById из DF (там — TODO), реализован у микросервиса.
/// </summary>
public sealed class RecordEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly JsonSerializerOptions _json;

    public RecordEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _json = new JsonSerializerOptions();
        _json.Converters.Add(new IValueObjectJsonConverter());
    }

    public async Task<IJournalAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
    {
        var events = await _db.Events
            .Where(e => e.Id == id)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        if (events.Count == 0) return null;

        var model = new AnemicModel { Id = id };
        foreach (var e in events)
        {
            var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(e.ChangedValueObjectsJson, _json);
            if (vos is null || vos.Count == 0) continue;
            model = Apply(model, vos, e);
        }

        return model;
    }

    private static AnemicModel Apply(AnemicModel m, IDictionary<string, IValueObject> vos, DomainEventEntry e)
    {
        if (vos.TryGetValue("Root", out var r) && r is IRecordRoot root) m = m with { Root = root };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        if (vos.TryGetValue("Place", out var p) && p is PlaceValue pv) m = m with { Place = pv.Value };
        if (vos.TryGetValue("Positions", out var po) && po is PositionsValue pov) m = m with { Positions = pov.Value };
        if (vos.TryGetValue("Accessories", out var a) && a is AccessoriesValue av) m = m with { Costumes = av.Costumes, Toys = av.Toys, Fantasies = av.Fantasies, Tags = av.Tags };
        if (vos.TryGetValue("Contraception", out var cn) && cn is ContraceptionValue cnv) m = m with { Contraceptives = cnv.Contraceptives, ContraceptiveCount = cnv.ContraceptiveCount };
        if (vos.TryGetValue("Duration", out var d) && d is DurationValue dv) m = m with { DurationMin = dv.Minutes };
        if (vos.TryGetValue("Orgasms", out var o) && o is OrgasmsListValueObject ov) m = m with { Orgasms = ov.Value };
        if (vos.TryGetValue("Origin", out var or) && or is OriginValue orv) m = m with { Garmin = orv.Garmin, GarminId = orv.GarminId };
        if (vos.TryGetValue("State", out var s) && s is IntValueObject sv) m = m with { State = (RecordState)sv.Value };
        if (vos.TryGetValue("Deleted", out var del) && del is BoolValueObject dvb) m = m with { Deleted = dvb.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
