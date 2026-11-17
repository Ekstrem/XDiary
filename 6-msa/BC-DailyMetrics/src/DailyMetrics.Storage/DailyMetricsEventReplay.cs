namespace DailyMetrics.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using XDiary.Contracts;

/// <summary>Реальный replay из событий: сворачивает ChangedValueObjects в AnemicModel.</summary>
public sealed class DailyMetricsEventReplay
{
    private readonly EventStoreDbContext _db;
    private readonly JsonSerializerOptions _json;
    public DailyMetricsEventReplay(EventStoreDbContext db)
    {
        _db = db;
        _json = new JsonSerializerOptions();
        _json.Converters.Add(new IValueObjectJsonConverter());
    }

    public async Task<IDailyMetricsAnemicModel?> ReplayAsync(Guid id, CancellationToken ct = default)
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
        if (vos.TryGetValue("Root", out var r) && r is IDailyMetricsRoot root) m = m with { Root = root };
        if (vos.TryGetValue("Stress", out var s) && s is IntValueObject sv) m = m with { Stress = sv.Value };
        if (vos.TryGetValue("RestingHr", out var rh) && rh is IntValueObject rhv) m = m with { RestingHr = rhv.Value };
        if (vos.TryGetValue("Hrv", out var h) && h is IntValueObject hv) m = m with { Hrv = hv.Value };
        if (vos.TryGetValue("Sleep", out var sl) && sl is ISleep sleep) m = m with { Sleep = sleep };
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m = m with { ClientId = cv.Value };
        return m with { Version = e.Version, CorrelationToken = e.CorrelationToken };
    }
}
