namespace Privacy.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class PrivacyEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public PrivacyEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerFactory.CreateDefault();
    }

    public async Task<IPinCredentialAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
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
        if (vos.TryGetValue("Root", out var r) && r is IPinCredentialRoot root) m = m with { Root = root };
        if (vos.TryGetValue("Locked", out var l) && l is BoolValueObject lv) m = m with { Locked = lv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
