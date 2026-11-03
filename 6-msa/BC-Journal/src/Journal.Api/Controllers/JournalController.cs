namespace Journal.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Application.Commands;
using Journal.Application.Queries;
using Journal.Domain;
using Journal.Domain.Abstraction;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/journal")]
[Authorize]
public sealed class JournalController : ControllerBase
{
    private readonly IMediator _mediator;
    public JournalController(IMediator mediator) => _mediator = mediator;

    private IActionResult Respond(AggregateResult<IJournal, IJournalAnemicModel> r)
        => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    private bool TryClientId(out Guid clientId)
    {
        clientId = Guid.Empty;
        var sub = User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out clientId);
    }

    private async Task<IActionResult?> EnsureOwns(Guid id, Guid clientId)
    {
        var rec = await _mediator.Send(new GetRecordByIdQuery(id));
        if (rec is null) return NotFound();
        return rec.ClientId == clientId ? null : Forbid();
    }

    private async Task<IActionResult> WithOwner(Guid id, Func<Task<IActionResult>> action)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        var guard = await EnsureOwns(id, clientId);
        return guard ?? await action();
    }

    [HttpPost("records")]
    public async Task<IActionResult> StartManualEntry(CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        return Respond(await _mediator.Send(new StartManualEntryCommand(clientId), ct));
    }
    [HttpPost("records/from-garmin")]
    [AllowAnonymous]
    public async Task<IActionResult> StartFromGarmin([FromBody] StartFromGarminCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("records/{id:guid}/when")]
    public Task<IActionResult> SpecifyWhen(Guid id, [FromBody] SpecifyWhenCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/partners")]
    public Task<IActionResult> AddPartner(Guid id, [FromBody] AddPartnerCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/partners/{partnerId}/remove")]
    public Task<IActionResult> RemovePartner(Guid id, string partnerId, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(new RemovePartnerCommand(id, partnerId), ct)));
    [HttpPost("records/{id:guid}/place")]
    public Task<IActionResult> SpecifyPlace(Guid id, [FromBody] SpecifyPlaceCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/positions")]
    public Task<IActionResult> AddPositions(Guid id, [FromBody] AddPositionsCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/accessories")]
    public Task<IActionResult> AddAccessories(Guid id, [FromBody] AddAccessoriesCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/contraception")]
    public Task<IActionResult> RecordContraception(Guid id, [FromBody] RecordContraceptionCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/duration")]
    public Task<IActionResult> SpecifyDuration(Guid id, [FromBody] SpecifyDurationCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/orgasms")]
    public Task<IActionResult> RecordOrgasms(Guid id, [FromBody] RecordOrgasmsCommand c, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(c with { AggregateId = id }, ct)));
    [HttpPost("records/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(new CompleteCommand(id), ct)));
    [HttpPost("records/{id:guid}/delete")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => WithOwner(id, async () => Respond(await _mediator.Send(new DeleteRecordCommand(id), ct)));
    [HttpGet("records")]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        return Ok(await _mediator.Send(new ListRecordsQuery(clientId, limit, offset), ct));
    }
    [HttpGet("recent")]
    public async Task<IActionResult> Recent(CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        return Ok(await _mediator.Send(new RecentDefaultsQuery(clientId), ct));
    }
    [HttpGet("places")]
    public async Task<IActionResult> Places(CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        return Ok(await _mediator.Send(new JournalPlacesQuery(clientId), ct));
    }
    [HttpGet("fantasies")]
    public async Task<IActionResult> Fantasies(CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        return Ok(await _mediator.Send(new JournalFantasiesQuery(clientId), ct));
    }
    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!TryClientId(out var clientId)) return Unauthorized();
        var r = await _mediator.Send(new GetRecordByIdQuery(id), ct);
        if (r is null) return NotFound();
        return r.ClientId == clientId ? Ok(r) : Forbid();
    }
}
