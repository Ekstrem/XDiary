using SomaticSeries.Application;
using SomaticSeries.Application.Commands;
using SomaticSeries.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SomaticSeries.Api.Controllers;

/// <summary>
/// Command/Query API соматики (минутные точки): /api/v1/somatic-series.
/// </summary>
[ApiController]
[Route("api/v1/somatic-series")]
public class SomaticSeriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SomaticSeriesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Импортировать минутную точку (upsert по t).</summary>
    [HttpPost]
    public async Task<ActionResult<SomaticSeriesOperationResult>> Import(
        [FromBody] ImportPointCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Срез соматики вокруг startedAt (±WindowMin минут).</summary>
    [HttpGet("slice")]
    public async Task<IActionResult> Slice(
        [FromQuery] string startedAt, [FromQuery] int windowMin = 15, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSomaticSliceQuery { StartedAt = startedAt, WindowMin = windowMin }, ct);
        return Ok(result);
    }
}
