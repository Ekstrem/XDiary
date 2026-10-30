namespace AppleHealthGateway.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using AppleHealthGateway.Application.Commands;
using AppleHealthGateway.Application.Queries;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/apple-health")]
public sealed class AppleHealthGatewayController : ControllerBase
{
    private readonly IMediator _mediator;
    public AppleHealthGatewayController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost("configure")]
    public async Task<IActionResult> Configure([FromBody] ConfigureCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("import")]
    public async Task<IActionResult> ImportFile([FromBody] ImportFileCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpGet("state")]
    public async Task<IActionResult> State([FromQuery] string sourceId, CancellationToken ct)
    { var r = await _mediator.Send(new GetImportStateQuery(sourceId), ct); return r is null ? NotFound() : Ok(r); }
}