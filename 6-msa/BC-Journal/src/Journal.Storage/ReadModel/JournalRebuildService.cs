using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Journal.Storage.ReadModel;

/// <summary>
/// РџРµСЂРµСЃС‚СЂР°РёРІР°РµС‚ РїСЂРѕРµРєС†РёРё JournalReadModel РёР· Command Store (Event Sourcing).
/// РРґРµРјРїРѕС‚РµРЅС‚РЅРѕ: СЃРѕР±С‹С‚РёСЏ СЃ Version <= С‚РµРєСѓС‰РµР№ РІРµСЂСЃРёРё РїСЂРѕРµРєС†РёРё РїСЂРѕРїСѓСЃРєР°СЋС‚СЃСЏ.
/// </summary>
public class JournalRebuildService : IRebuildService<IJournal>
{
    private readonly CommandDbContext _commandDb;
    private readonly JournalProjectionHandler _projectionHandler;
    private readonly ILogger<JournalRebuildService> _logger;

    public JournalRebuildService(
        CommandDbContext commandDb,
        JournalProjectionHandler projectionHandler,
        ILogger<JournalRebuildService> logger)
    {
        _commandDb = commandDb;
        _projectionHandler = projectionHandler;
        _logger = logger;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ JournalReadModel: РЅР°С‡Р°Р»Рѕ.");

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

        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ JournalReadModel Р·Р°РІРµСЂС€С‘РЅ: {Count} СЃРѕР±С‹С‚РёР№.", projected);
    }
}
