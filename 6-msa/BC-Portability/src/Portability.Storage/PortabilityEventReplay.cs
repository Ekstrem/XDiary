namespace Portability.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class PortabilityEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly JsonSerializerOptions _json;
    public PortabilityEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _json = new JsonSerializerOptions();
        _json.Converters.Add(new IValueObjectJsonConverter());
    }

    public async Task<ICsvDocumentAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
    {
        var events = await _db.Events.Where(e => e.Id == id).OrderBy(e => e.Version).ToListAsync(ct);
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

    internal static AnemicModel Apply(AnemicModel m, IDictionary<string, IValueObject> vos, DomainEventEntry e)
    {
        if (vos.TryGetValue("Root", out var r) && r is ICsvDocumentRoot root) m = m with { Root = root };
        if (vos.TryGetValue("Rows", out var rows) && rows is StringListValueObject rv) m = m with { Rows = rv.Value };
        if (vos.TryGetValue("Kind", out var k) && k is StringValueObject kv) m = m with { Kind = kv.Value };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
