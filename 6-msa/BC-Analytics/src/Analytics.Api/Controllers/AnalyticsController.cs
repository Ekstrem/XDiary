using Analytics.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

/// <summary>
/// Query API аналитики: /api/v1/analytics.
/// Read-only: нет команд записи, только вычисляемые проекции.
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Сводка за период (30/90/all).</summary>
    [HttpPost("summary")]
    public async Task<IActionResult> Summary([FromBody] GetSummaryQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>Корреляции (Pearson) между записями и дневными метриками (n ≥ 3).</summary>
    [HttpPost("correlations")]
    public async Task<IActionResult> Correlations([FromBody] GetCorrelationsQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
