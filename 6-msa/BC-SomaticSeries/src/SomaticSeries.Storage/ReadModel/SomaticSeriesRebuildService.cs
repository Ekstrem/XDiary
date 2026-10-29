using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using SomaticSeries.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SomaticSeries.Storage.ReadModel;

/// <summary>
/// РџРµСЂРµСЃС‚СЂР°РёРІР°РµС‚ РїСЂРѕРµРєС†РёРё SomaticSeriesReadModel РёР· Command Store (Event Sourcing).
/// РРґРµРјРїРѕС‚РµРЅС‚РЅРѕ: СЃРѕР±С‹С‚РёСЏ СЃ Version <= С‚РµРєСѓС‰РµР№ РІРµСЂСЃРёРё РїСЂРѕРµРєС†РёРё РїСЂРѕРїСѓСЃРєР°СЋС‚СЃСЏ.
/// </summary>
public class SomaticSeriesRebuildService : IRebuildService<ISomaticSeries>
{
    private readonly CommandDbContext _commandDb;
    private readonly SomaticSeriesProjectionHandler _projectionHandler;
    private readonly ILogger<SomaticSeriesRebuildService> _logger;

    public SomaticSeriesRebuildService(
        CommandDbContext commandDb,
        SomaticSeriesProjectionHandler projectionHandler,
        ILogger<SomaticSeriesRebuildService> logger)
    {
        _commandDb = commandDb;
        _projectionHandler = projectionHandler;
        _logger = logger;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ SomaticSeriesReadModel: РЅР°С‡Р°Р»Рѕ.");

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

        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ SomaticSeriesReadModel Р·Р°РІРµСЂС€С‘РЅ: {Count} СЃРѕР±С‹С‚РёР№.", projected);
    }
}
