using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Portability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Portability.Storage.ReadModel;

/// <summary>
/// РџРµСЂРµСЃС‚СЂР°РёРІР°РµС‚ РїСЂРѕРµРєС†РёРё CsvDocumentReadModel РёР· Command Store (Event Sourcing).
/// РРґРµРјРїРѕС‚РµРЅС‚РЅРѕ: СЃРѕР±С‹С‚РёСЏ СЃ Version <= С‚РµРєСѓС‰РµР№ РІРµСЂСЃРёРё РїСЂРѕРµРєС†РёРё РїСЂРѕРїСѓСЃРєР°СЋС‚СЃСЏ.
/// </summary>
public class PortabilityRebuildService : IRebuildService<IPortability>
{
    private readonly CommandDbContext _commandDb;
    private readonly PortabilityProjectionHandler _projectionHandler;
    private readonly ILogger<PortabilityRebuildService> _logger;

    public PortabilityRebuildService(
        CommandDbContext commandDb,
        PortabilityProjectionHandler projectionHandler,
        ILogger<PortabilityRebuildService> logger)
    {
        _commandDb = commandDb;
        _projectionHandler = projectionHandler;
        _logger = logger;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ CsvDocumentReadModel: РЅР°С‡Р°Р»Рѕ.");

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

        _logger.LogInformation("Rebuild РїСЂРѕРµРєС†РёР№ CsvDocumentReadModel Р·Р°РІРµСЂС€С‘РЅ: {Count} СЃРѕР±С‹С‚РёР№.", projected);
    }
}
