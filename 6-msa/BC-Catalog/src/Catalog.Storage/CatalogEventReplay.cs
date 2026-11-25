namespace Catalog.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class CatalogEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public CatalogEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerProvider.Create();
    }

    public async Task<ICatalogAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
    {
        var events = await _db.Events.Where(e => e.Id == id).OrderBy(e => e.Version).ToListAsync(ct);
        if (events.Count == 0) return null;
        var model = new AnemicModel { Id = id };
        foreach (var e in events)
        {
            var vos = ValueObjectSerializerProvider.DeserializeWithLegacy(_serializer, e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0) continue;
            model = Apply(model, vos, e);
        }
        return model;
    }

    internal static AnemicModel Apply(AnemicModel m, IReadOnlyDictionary<string, IValueObject> vos, DomainEventEntry e)
    {
        if (vos.TryGetValue("Root", out var r) && r is ICatalogItemRoot root) m = m with { Root = root };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        if (vos.TryGetValue("Photo", out var p) && p is StringValueObject pv) m = m with { Photo = pv.Value };
        if (vos.TryGetValue("Notes", out var n) && n is StringValueObject nv) m = m with { Notes = nv.Value };
        if (vos.TryGetValue("Brand", out var b) && b is StringValueObject bv) m = m with { Brand = bv.Value };
        if (vos.TryGetValue("Archived", out var a) && a is BoolValueObject av) m = m with { Archived = av.Value };
        if (vos.TryGetValue("FantasyPartner", out var f) && f is StringValueObject fv) m = m with { FantasyPartner = fv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
