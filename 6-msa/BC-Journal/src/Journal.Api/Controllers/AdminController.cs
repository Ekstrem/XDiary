using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Journal.Api.Controllers;

/// <summary>
/// РђРґРјРёРЅ-РѕРїРµСЂР°С†РёРё Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
[ApiController]
[Route("api/v1/Journal/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IJournal> _rebuildService;

    public AdminController(IRebuildService<IJournal> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// РџРµСЂРµСЃС‚СЂРѕРёС‚СЊ РїСЂРѕРµРєС†РёРё JournalReadModel РёР· Event Store (РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild РїСЂРѕРµРєС†РёР№ Р·Р°РІРµСЂС€С‘РЅ." });
    }
}
