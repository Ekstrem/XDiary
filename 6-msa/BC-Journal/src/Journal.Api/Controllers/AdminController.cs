using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Journal.Api.Controllers;

/// <summary>
/// Админ-операции журнала.
/// </summary>
[ApiController]
[Route("api/v1/journal/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IJournal> _rebuildService;

    public AdminController(IRebuildService<IJournal> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// Перестроить проекции JournalReadModel из Event Store (идемпотентно).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild проекций завершён." });
    }
}
