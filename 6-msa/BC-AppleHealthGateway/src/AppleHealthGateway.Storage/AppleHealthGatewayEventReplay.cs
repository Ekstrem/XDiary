namespace AppleHealthGateway.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class AppleHealthGatewayEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly IValueObjectSerializer _serializer;
    public AppleHealthGatewayEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _serializer = ValueObjectSerializerFactory.CreateDefault();
    }

    public async Task<IAppleHealthGatewayAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
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
        if (vos.TryGetValue("Root", out var r) && r is IAppleImportRoot root) m = m with { Root = root };
        if (vos.TryGetValue("ImportConfig", out var ic) && ic is IImportConfig cfg) m = m with { ImportConfig = cfg };
        if (vos.TryGetValue("LastImport", out var li) && li is StringValueObject lv) m = m with { LastImport = lv.Value };
        if (vos.TryGetValue("ProcessedFiles", out var pf) && pf is StringListValueObject pv) m = m with { ProcessedFiles = pv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
