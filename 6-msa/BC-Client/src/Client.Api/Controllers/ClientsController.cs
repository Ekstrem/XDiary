namespace Client.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Client.Application.Commands;
using Client.Application.Queries;
using Client.Domain;
using Client.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ClientsController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IClients, IClientAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterClientCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyClientOtpCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("{id:guid}/link-partner")]
    public async Task<IActionResult> LinkPartner(Guid id, [FromBody] LinkClientPartnerCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("{id:guid}/unlink-partner")]
    public async Task<IActionResult> UnlinkPartner(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new UnlinkClientPartnerCommand(id), ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListClientsQuery(), ct));
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (sub is null || !Guid.TryParse(sub, out var id)) return Unauthorized();
        var r = await _mediator.Send(new GetClientByIdQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new GetClientByIdQuery(id), ct); return r is null ? NotFound() : Ok(r); }
}