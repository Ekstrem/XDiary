using AppleHealthGateway.Application;
using AppleHealthGateway.Application.Commands;
using AppleHealthGateway.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AppleHealthGateway.Api.Controllers;

/// <summary>
/// Command/Query API шлюза Apple Health: /api/v1/apple-health-gateway.
/// </summary>
[ApiController]
[Route("api/v1/apple-health-gateway")]
public class AppleHealthGatewayController : ControllerBase
{
    private readonly IMediator _mediator;

    public AppleHealthGatewayController(IMediator mediator) => _mediator = mediator;

    /// <summary>Сохранить состояние импорта (upsert по sourceId).</summary>
    [HttpPost]
    public async Task<ActionResult<AppleHealthGatewayOperationResult>> Upsert(
        [FromBody] UpsertImportStateCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Состояние импорта по источнику.</summary>
    [HttpGet("state")]
    public async Task<IActionResult> State(
        [FromQuery] string sourceId = "apple-health", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetImportStateQuery { SourceId = sourceId }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}
