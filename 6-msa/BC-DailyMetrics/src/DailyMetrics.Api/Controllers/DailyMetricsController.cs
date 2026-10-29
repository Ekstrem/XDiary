using DailyMetrics.Application;
using DailyMetrics.Application.Commands;
using DailyMetrics.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DailyMetrics.Api.Controllers;

/// <summary>
/// Command/Query API дневных метрик: /api/v1/daily-metrics.
/// </summary>
[ApiController]
[Route("api/v1/daily-metrics")]
public class DailyMetricsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DailyMetricsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Импортировать дневную сводку (upsert по date).</summary>
    [HttpPost]
    public async Task<ActionResult<DailyMetricsOperationResult>> Import(
        [FromBody] ImportDailyCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Дневные тренды в диапазоне дат.</summary>
    [HttpGet("series")]
    public async Task<IActionResult> Series(
        [FromQuery] string fromDate, [FromQuery] string toDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDailySeriesQuery { FromDate = fromDate, ToDate = toDate }, ct);
        return Ok(result);
    }
}
