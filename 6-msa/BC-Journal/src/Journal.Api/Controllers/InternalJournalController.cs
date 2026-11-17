namespace Journal.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("internal/journal")]
[Authorize]
public sealed class InternalJournalController : ControllerBase
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    public InternalJournalController(IReadRepository<IJournal, JournalReadModel> read) => _read = read;
    [HttpGet("records")]
    public async Task<IActionResult> ListAll([FromQuery] Guid clientId, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        IEnumerable<JournalReadModel> q = all.Where(x => x.ClientId == clientId);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        return Ok(q.ToList());
    }
    [HttpGet("client-ids")]
    public async Task<IActionResult> ClientIds(CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        return Ok(all.Where(x => x.ClientId is not null).Select(x => x.ClientId!.Value).Distinct().ToList());
    }
}
