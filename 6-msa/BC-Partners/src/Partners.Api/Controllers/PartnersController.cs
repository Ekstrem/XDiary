namespace Partners.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Partners.Application.Commands;
using Partners.Application.Queries;
using Partners.Domain;
using Partners.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/partners")]
public sealed class PartnersController : ControllerBase
{
    private readonly IMediator _mediator;
    public PartnersController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IPartners, IPartnerAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterPartnerCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenamePartnerCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPut("{id:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdatePartnerProfileCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new RemovePartnerCommand(id), ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListPartnersQuery(), ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new GetPartnerByIdQuery(id), ct); return r is null ? NotFound() : Ok(r); }
}