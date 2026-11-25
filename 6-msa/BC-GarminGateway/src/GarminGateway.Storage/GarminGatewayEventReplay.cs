namespace GarminGateway.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class GarminGatewayEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public GarminGatewayEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerProvider.Create();
    }

    public async Task<IGarminGatewayAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
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
        if (vos.TryGetValue("Root", out var r) && r is IGarminSyncRoot root) m = m with { Root = root };
        if (vos.TryGetValue("Credentials", out var c) && c is ICredentials creds) m = m with { Credentials = creds };
        if (vos.TryGetValue("SyncConfig", out var sc) && sc is ISyncConfig cfg) m = m with { SyncConfig = cfg };
        if (vos.TryGetValue("LastSync", out var ls) && ls is StringValueObject lv) m = m with { LastSync = lv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
