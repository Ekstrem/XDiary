using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using GarminGateway.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GarminGateway.Api.Controllers;

/// <summary>
/// РђРґРјРёРЅ-РѕРїРµСЂР°С†РёРё Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
[ApiController]
[Route("api/v1/GarminGateway/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IGarminGateway> _rebuildService;

    public AdminController(IRebuildService<IGarminGateway> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// РџРµСЂРµСЃС‚СЂРѕРёС‚СЊ РїСЂРѕРµРєС†РёРё GarminSyncReadModel РёР· Event Store (РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild РїСЂРѕРµРєС†РёР№ Р·Р°РІРµСЂС€С‘РЅ." });
    }
}
