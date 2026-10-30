namespace Journal.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Application.Commands;
using Journal.Application.Queries;
using Journal.Domain;
using Journal.Domain.Abstraction;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/journal")]
public sealed class JournalController : ControllerBase
{
    private readonly IMediator _mediator;
    public JournalController(IMediator mediator) => _mediator = mediator;

    private IActionResult Respond(AggregateResult<IJournal, IJournalAnemicModel> r)
        => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost("records")]
    public async Task<IActionResult> StartManualEntry(CancellationToken ct) => Respond(await _mediator.Send(new StartManualEntryCommand(), ct));
    [HttpPost("records/from-garmin")]
    public async Task<IActionResult> StartFromGarmin([FromBody] StartFromGarminCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("records/{id:guid}/when")]
    public async Task<IActionResult> SpecifyWhen(Guid id, [FromBody] SpecifyWhenCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/partners")]
    public async Task<IActionResult> AddPartner(Guid id, [FromBody] AddPartnerCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpDelete("records/{id:guid}/partners/{partnerId}")]
    public async Task<IActionResult> RemovePartner(Guid id, string partnerId, CancellationToken ct) => Respond(await _mediator.Send(new RemovePartnerCommand(id, partnerId), ct));
    [HttpPost("records/{id:guid}/place")]
    public async Task<IActionResult> SpecifyPlace(Guid id, [FromBody] SpecifyPlaceCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/positions")]
    public async Task<IActionResult> AddPositions(Guid id, [FromBody] AddPositionsCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/accessories")]
    public async Task<IActionResult> AddAccessories(Guid id, [FromBody] AddAccessoriesCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/contraception")]
    public async Task<IActionResult> RecordContraception(Guid id, [FromBody] RecordContraceptionCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/duration")]
    public async Task<IActionResult> SpecifyDuration(Guid id, [FromBody] SpecifyDurationCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/orgasms")]
    public async Task<IActionResult> RecordOrgasms(Guid id, [FromBody] RecordOrgasmsCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("records/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new CompleteCommand(id), ct));
    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new DeleteRecordCommand(id), ct));
    [HttpGet("records")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListRecordsQuery(), ct));
    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetRecordByIdQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }
}