namespace Journal.Api.Controllers;
using Journal.InternalContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("internal/journal")]
[Authorize]
public sealed class InternalJournalController : ControllerBase
{
    private readonly IJournalQueryRepository _repo;
    public InternalJournalController(IJournalQueryRepository repo) => _repo = repo;
    [HttpGet("records")]
    public async Task<IActionResult> ListAll([FromQuery] Guid clientId, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
        => Ok(await _repo.ListAsync(clientId, limit, offset, ct));
    [HttpGet("client-ids")]
    public async Task<IActionResult> ClientIds(CancellationToken ct)
        => Ok(await _repo.GetClientIdsAsync(ct));
}