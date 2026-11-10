namespace Journal.Api.Controllers;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// E1 (срез Partner.Removed → partners_directory): внутренний приём событий Partners
/// и проекция справочника имён (upsert по partnerId). Зона доверия — ADR-008.
/// </summary>
[ApiController]
[Route("internal/partners")]
[Authorize]
public sealed class PartnersEventsController : ControllerBase
{
    private readonly IReadModelStore<IJournal, PartnersDirectoryReadModel> _store;
    private readonly ILogger<PartnersEventsController> _log;
    public PartnersEventsController(IReadModelStore<IJournal, PartnersDirectoryReadModel> store, ILogger<PartnersEventsController> log) { _store = store; _log = log; }

    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostEvent([FromBody] PartnersEventDto dto, CancellationToken ct)
    {
        // BE-082: внутренняя запись в read-модель — только сервис-токен sub=partners (ADR-008).
        var sub = User.FindFirst("sub")?.Value;
        _log.LogInformation("internal/partners/events: sub={Sub} partnerId={PartnerId}", sub, dto.PartnerId);
        if (sub != "partners") return Forbid();
        // @TL 17:42: внутренний контракт — писать без имени нельзя; пустое имя → 400, а не молчаливое "".
        if (string.IsNullOrEmpty(dto.Name)) return BadRequest(new { error = "name required" });
        try
        {
            await _store.UpsertAsync(new PartnersDirectoryReadModel
            {
                Id = dto.PartnerId,
                Name = dto.Name,
                ClientId = dto.ClientId,
                DeletedAt = dto.DeletedAt,
            }, ct);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "internal/partners/events: upsert failed partnerId={PartnerId}", dto.PartnerId);
            throw;
        }
    }
}

public sealed record PartnersEventDto(Guid PartnerId, string? Name, Guid? ClientId, DateTime? DeletedAt);
