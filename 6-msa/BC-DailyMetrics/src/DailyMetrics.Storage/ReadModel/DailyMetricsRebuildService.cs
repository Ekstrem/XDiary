using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using DailyMetrics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DailyMetrics.Storage.ReadModel;

/// <summary>
/// РџРµСЂРµСЃС‚СЂР°РёРІР°РµС‚ РїСЂРѕРµРєС†РёРё DailyMetricsReadModel РёР· Command Store (Event Sourcing).
/// РРґРµРјРїРѕС‚РµРЅС‚РЅРѕ: СЃРѕР±С‹С‚РёСЏ СЃ Version <= С‚РµРєСѓС‰РµР№ РІРµСЂСЃРёРё РїСЂРѕРµРєС†РёРё РїСЂРѕРїСѓСЃРєР°СЋС‚СЃСЏ.
/// </summary>
public class DailyMetricsRebuildService : IRebuildService<IDailyMetrics>
{
    private readonly CommandDbContext _commandDb;
    private readonly DailyMetricsProjectionHandler _projectionHandler;
    private readonly ILogger<DailyMetricsRebuildService> _logger;

    public DailyMetricsRebuildService(
        CommandDbContext commandDb,
        DailyMetricsProjectionHandler projectionHandler,
        ILogger<DailyMetricsRebuildService> logger)
    {
        _commandDb = commandDb;
        _projectionHandler = projectionHandler;
        _logger = logger;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ DailyMetricsReadModel: РЅР°С‡Р°Р»Рѕ.");

        var events = await _commandDb.Events
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        _logger.LogInformation("Rebuild: РЅР°Р№РґРµРЅРѕ {Count} СЃРѕР±С‹С‚РёР№ РІ Event Store.", events.Count);

        var projected = 0;
        foreach (var entry in events)
        {
            await _projectionHandler.ProjectFromEntryAsync(entry, ct);
            projected++;
        }

        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ DailyMetricsReadModel Р·Р°РІРµСЂС€С‘РЅ: {Count} СЃРѕР±С‹С‚РёР№.", projected);
    }
}
