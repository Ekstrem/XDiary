namespace SomaticSeries.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class SomaticSeriesEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public SomaticSeriesEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerFactory.CreateDefault();
    }

    public async Task<ISomaticSeriesAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
    {
        var events = await _db.Events.Where(e => e.Id == id).OrderBy(e => e.Version).ToListAsync(ct);
        if (events.Count == 0) return null;
        var model = new AnemicModel { Id = id };
        foreach (var e in events)
        {
            var vos = _serializer.DeserializeWithLegacy(e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0) continue;
            model = Apply(model, vos, e);
        }
        return model;
    }

    internal static AnemicModel Apply(AnemicModel m, IReadOnlyDictionary<string, IValueObject> vos, DomainEventEntry e)
    {
        if (vos.TryGetValue("Root", out var r) && r is ISomaticPointRoot root) m = m with { Root = root };
        if (vos.TryGetValue("Hr", out var h) && h is IntValueObject hv) m = m with { Hr = hv.Value };
        if (vos.TryGetValue("BodyBattery", out var b) && b is IntValueObject bv) m = m with { BodyBattery = bv.Value };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
