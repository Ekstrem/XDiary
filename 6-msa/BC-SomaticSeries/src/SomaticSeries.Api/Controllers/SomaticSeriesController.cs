namespace SomaticSeries.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SomaticSeries.Application.Commands;
using SomaticSeries.Application.Queries;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/somatic-series")]
public sealed class SomaticSeriesController : ControllerBase
{
    private readonly IMediator _mediator;
    public SomaticSeriesController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost("points")]
    public async Task<IActionResult> RecordPoint([FromBody] RecordPointCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpGet("slice")]
    public async Task<IActionResult> Slice([FromQuery] string fromT, [FromQuery] string toT, CancellationToken ct) => Ok(await _mediator.Send(new GetSomaticSliceQuery(fromT, toT), ct));
}