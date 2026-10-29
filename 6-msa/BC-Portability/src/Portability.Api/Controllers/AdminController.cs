using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Portability.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Portability.Api.Controllers;

/// <summary>
/// РђРґРјРёРЅ-РѕРїРµСЂР°С†РёРё Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
[ApiController]
[Route("api/v1/Portability/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IPortability> _rebuildService;

    public AdminController(IRebuildService<IPortability> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// РџРµСЂРµСЃС‚СЂРѕРёС‚СЊ РїСЂРѕРµРєС†РёРё CsvDocumentReadModel РёР· Event Store (РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild РїСЂРѕРµРєС†РёР№ Р·Р°РІРµСЂС€С‘РЅ." });
    }
}
