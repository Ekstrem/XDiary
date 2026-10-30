namespace DailyMetrics.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using DailyMetrics.Application.Commands;
using DailyMetrics.Application.Queries;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/daily-metrics")]
public sealed class DailyMetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DailyMetricsController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost("stress")]
    public async Task<IActionResult> Stress([FromBody] RecordStressCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("resting-hr")]
    public async Task<IActionResult> RestingHr([FromBody] RecordRestingHrCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("hrv")]
    public async Task<IActionResult> Hrv([FromBody] RecordHrvCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("sleep")]
    public async Task<IActionResult> Sleep([FromBody] RecordSleepCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpGet("series")]
    public async Task<IActionResult> Series([FromQuery] string fromDate, [FromQuery] string toDate, CancellationToken ct) => Ok(await _mediator.Send(new GetDailySeriesQuery(fromDate, toDate), ct));
}