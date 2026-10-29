using GarminGateway.Application;
using GarminGateway.Application.Commands;
using GarminGateway.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GarminGateway.Api.Controllers;

/// <summary>
/// Command/Query API шлюза Garmin: /api/v1/garmin-gateway.
/// </summary>
[ApiController]
[Route("api/v1/garmin-gateway")]
public class GarminGatewayController : ControllerBase
{
    private readonly IMediator _mediator;

    public GarminGatewayController(IMediator mediator) => _mediator = mediator;

    /// <summary>Сохранить состояние синка (upsert по sourceId).</summary>
    [HttpPost]
    public async Task<ActionResult<GarminGatewayOperationResult>> Upsert(
        [FromBody] UpsertSyncStateCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Состояние синка по источнику.</summary>
    [HttpGet("state")]
    public async Task<IActionResult> State(
        [FromQuery] string sourceId = "garmin", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSyncStateQuery { SourceId = sourceId }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}
