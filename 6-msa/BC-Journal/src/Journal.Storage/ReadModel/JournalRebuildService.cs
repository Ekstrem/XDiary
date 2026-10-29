using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Journal.Storage.ReadModel;

/// <summary>
/// Перестраивает проекции JournalReadModel из Command Store (Event Sourcing).
/// Идемпотентно: события с Version <= текущей версии проекции пропускаются.
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
        _logger.LogInformation("Rebuild проекций JournalReadModel: начало.");

        var events = await _commandDb.Events
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        _logger.LogInformation("Rebuild: найдено {Count} событий в Event Store.", events.Count);

        var projected = 0;
        foreach (var entry in events)
        {
            await _projectionHandler.ProjectFromEntryAsync(entry, ct);
            projected++;
        }

        _logger.LogInformation("Rebuild проекций JournalReadModel завершён: {Count} событий.", projected);
    }
}
