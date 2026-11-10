namespace SomaticSeries.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SomaticSeries.Application.Commands;
using SomaticSeries.Application.Queries;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/somatic-series")]
[Authorize]
public sealed class SomaticSeriesController : ControllerBase
{
    private readonly IMediator _mediator;
    public SomaticSeriesController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    // BE-084: владелец точек — sub из токена; хранение и выдача привязаны к клиенту.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    [HttpPost("points")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordPoint([FromBody] RecordPointCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }

    [HttpGet("slice")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Slice([FromQuery] string fromT, [FromQuery] string toT, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(await _mediator.Send(new GetSomaticSliceQuery(fromT, toT, clientId.Value), ct)); }
}
