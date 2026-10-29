using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Privacy.Api.Controllers;

/// <summary>
/// РђРґРјРёРЅ-РѕРїРµСЂР°С†РёРё Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
[ApiController]
[Route("api/v1/Privacy/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IPrivacy> _rebuildService;

    public AdminController(IRebuildService<IPrivacy> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// РџРµСЂРµСЃС‚СЂРѕРёС‚СЊ РїСЂРѕРµРєС†РёРё PrivacyReadModel РёР· Event Store (РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild РїСЂРѕРµРєС†РёР№ Р·Р°РІРµСЂС€С‘РЅ." });
    }
}
