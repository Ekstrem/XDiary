namespace Client.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class ClientEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public ClientEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerProvider.Create();
    }

    public async Task<IClientAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
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
        if (vos.TryGetValue("Root", out var r) && r is IClientRoot root) m = m with { Root = root };
        if (vos.TryGetValue("DisplayName", out var d) && d is StringValueObject dv) m = m with { DisplayName = dv.Value };
        if (vos.TryGetValue("Gender", out var g) && g is StringValueObject gv) m = m with { Gender = gv.Value };
        if (vos.TryGetValue("PhotoUrl", out var p) && p is NullableStringValueObject pv) m = m with { PhotoUrl = pv.Value };
        if (vos.TryGetValue("OtpCode", out var o) && o is StringValueObject ov) m = m with { OtpCode = ov.Value };
        if (vos.TryGetValue("OtpExpiresAt", out var x) && x is DateTimeValueObject xv) m = m with { OtpExpiresAt = xv.Value };
        if (vos.TryGetValue("OtpAttempts", out var a) && a is IntValueObject av) m = m with { OtpAttempts = av.Value };
        if (vos.TryGetValue("Verified", out var v) && v is BoolValueObject vv) m = m with { Verified = vv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
