namespace DailyMetrics.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyMetrics.Application.Commands;
using DailyMetrics.Application.Queries;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/daily-metrics")]
[Authorize]
public sealed class DailyMetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DailyMetricsController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    // BE-084: владелец метрик — sub из токена; хранение и выдача привязаны к клиенту.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    [HttpPost("stress")]
    public async Task<IActionResult> Stress([FromBody] RecordStressCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpPost("resting-hr")]
    public async Task<IActionResult> RestingHr([FromBody] RecordRestingHrCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpPost("hrv")]
    public async Task<IActionResult> Hrv([FromBody] RecordHrvCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpPost("sleep")]
    public async Task<IActionResult> Sleep([FromBody] RecordSleepCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpGet("series")]
    public async Task<IActionResult> Series([FromQuery] string fromDate, [FromQuery] string toDate, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(await _mediator.Send(new GetDailySeriesQuery(fromDate, toDate, clientId.Value), ct)); }
}
