namespace DailyMetrics.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyMetrics.Application.Queries;

/// <summary>BE-091: внутренний эндпоинт для Analytics — сервис-токен (sub=analytics) + обязательный clientId.</summary>
[ApiController]
[Route("internal/daily-metrics")]
[Authorize]
public sealed class InternalDailyMetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InternalDailyMetricsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Series([FromQuery] Guid clientId, [FromQuery] string fromDate, [FromQuery] string toDate, CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (sub != "analytics") return Forbid();
        return Ok(await _mediator.Send(new GetDailySeriesQuery(fromDate, toDate, clientId), ct));
    }
}
