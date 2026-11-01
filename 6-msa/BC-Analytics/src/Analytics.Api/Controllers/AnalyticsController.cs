using Analytics.Api.Precompute;
using Analytics.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

/// <summary>
/// Query API аналитики: /api/v1/analytics.
/// Read-only: нет команд записи, только вычисляемые проекции (MV precompute).
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AnalyticsSnapshot _snapshot;

    public AnalyticsController(IMediator mediator, AnalyticsSnapshot snapshot) { _mediator = mediator; _snapshot = snapshot; }

    /// <summary>Сводка за период (30/90/all) — из готовой MV (precompute).</summary>
    [HttpGet("summary")]
    public IActionResult Summary([FromQuery] string period = "30") => Ok(_snapshot.Get(period));

    /// <summary>Корреляции (Pearson) между записями и дневными метриками (n ≥ 3).</summary>
    [HttpPost("correlations")]
    public async Task<IActionResult> Correlations([FromBody] GetCorrelationsQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}