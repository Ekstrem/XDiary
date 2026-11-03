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
[AllowAnonymous]
public sealed class PartnersEventsController : ControllerBase
{
    private readonly IReadModelStore<IJournal, PartnersDirectoryReadModel> _store;
    public PartnersEventsController(IReadModelStore<IJournal, PartnersDirectoryReadModel> store) => _store = store;

    [HttpPost("events")]
    public async Task<IActionResult> PostEvent([FromBody] PartnersEventDto dto, CancellationToken ct)
    {
        await _store.UpsertAsync(new PartnersDirectoryReadModel
        {
            PartnerId = dto.PartnerId,
            Name = dto.Name,
            ClientId = dto.ClientId,
            DeletedAt = dto.DeletedAt,
        }, ct);
        return Ok(new { ok = true });
    }
}

public sealed record PartnersEventDto(Guid PartnerId, string Name, Guid? ClientId, DateTime? DeletedAt);
