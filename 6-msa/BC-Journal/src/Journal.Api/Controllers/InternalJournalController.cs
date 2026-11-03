namespace Journal.Api.Controllers;
using Journal.InternalContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("internal/journal")]
[AllowAnonymous]
public sealed class InternalJournalController : ControllerBase
{
    private readonly IJournalQueryRepository _repo;
    public InternalJournalController(IJournalQueryRepository repo) => _repo = repo;
    [HttpGet("records")]
    public async Task<IActionResult> ListAll([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
        => Ok(await _repo.ListAllAsync(limit, offset, ct));
}