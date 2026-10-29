using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Partners.Api.Controllers;

/// <summary>
/// РђРґРјРёРЅ-РѕРїРµСЂР°С†РёРё Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
[ApiController]
[Route("api/v1/Partners/admin")]
public class AdminController : ControllerBase
{
    private readonly IRebuildService<IPartners> _rebuildService;

    public AdminController(IRebuildService<IPartners> rebuildService) => _rebuildService = rebuildService;

    /// <summary>
    /// РџРµСЂРµСЃС‚СЂРѕРёС‚СЊ РїСЂРѕРµРєС†РёРё PartnersReadModel РёР· Event Store (РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ).
    /// </summary>
    [HttpPost("rebuild-projections")]
    public async Task<IActionResult> RebuildProjections(CancellationToken ct)
    {
        await _rebuildService.RebuildAsync(ct);
        return Ok(new { message = "Rebuild РїСЂРѕРµРєС†РёР№ Р·Р°РІРµСЂС€С‘РЅ." });
    }
}
