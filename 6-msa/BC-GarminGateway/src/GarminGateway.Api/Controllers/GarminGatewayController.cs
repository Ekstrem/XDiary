namespace GarminGateway.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using GarminGateway.Application.Commands;
using GarminGateway.Application.Queries;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/garmin")]
public sealed class GarminGatewayController : ControllerBase
{
    private readonly IMediator _mediator;
    public GarminGatewayController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());
    private Guid CurrentClientId() { var sub = User.FindFirst("sub")?.Value; return sub is not null && Guid.TryParse(sub, out var id) ? id : Guid.Empty; }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { ClientId = CurrentClientId() }, ct));
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { ClientId = CurrentClientId() }, ct));
    [HttpPost("fetch-activities")]
    public async Task<IActionResult> FetchActivities(CancellationToken ct) => Respond(await _mediator.Send(new FetchActivitiesCommand(CurrentClientId()), ct));
    [HttpGet("state")]
    public async Task<IActionResult> State(CancellationToken ct)
    { var r = await _mediator.Send(new GetSyncStateQuery(CurrentClientId()), ct); return r is null ? NotFound() : Ok(r); }
}