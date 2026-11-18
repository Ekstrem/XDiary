namespace Partners.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class PartnersEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly JsonSerializerOptions _json;

    public PartnersEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _json = new JsonSerializerOptions();
        _json.Converters.Add(new IValueObjectJsonConverter());
    }

    public async Task<IPartnerAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
    {
        var events = await _db.Events
            .Where(e => e.Id == id)
            .AsNoTracking()
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        if (events.Count == 0) return null;

        var model = new AnemicModel { Id = id };
        foreach (var e in events)
        {
            try
            {
                var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(e.ChangedValueObjectsJson, _json);
                if (vos is null || vos.Count == 0) continue;
                model = Apply(model, vos, e);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[replay] EX: " + ex);
                throw;
            }
        }

        return model;
    }

    internal static AnemicModel Apply(AnemicModel m, IDictionary<string, IValueObject> vos, DomainEventEntry e)
    {
        if (vos.TryGetValue("Root", out var r) && r is IPartnerRoot root) m = m with { Root = root };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        if (vos.TryGetValue("DeletedAt", out var d) && d is DeletedAtValueObject dv) m = m with { DeletedAt = dv.Value };
        if (vos.TryGetValue("Photo", out var p) && p is StringValueObject pv) m = m with { Photo = pv.Value };
        if (vos.TryGetValue("Gender", out var g) && g is StringValueObject gv) m = m with { Gender = gv.Value };
        if (vos.TryGetValue("Notes", out var n) && n is StringValueObject nv) m = m with { Notes = nv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
