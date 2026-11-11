namespace Partners.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
    private IActionResult Respond(AggregateResult<IPartners, IPartnerAnemicModel> r)
    {
        if (r.IsSuccess()) return Ok(r.Model());
        // BE-080: «нет агрегата» → 404, а не 400; валидация/состояние — 400.
        return r.ErrorMessage() == "Partner not found." ? NotFound() : BadRequest(r.ErrorMessage());
    }

    // BE-085 (IDOR): единый путь владельца — sub → clientId, иначе чужой объект доступен по одному id.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    private async Task<IActionResult?> OwnerGuardAsync(Guid id, Guid clientId, CancellationToken ct)
    {
        // BE-085: чужой id → 404 (не 403) — фильтр по clientId, не подтверждаем существование.
        var existing = await _mediator.Send(new GetPartnerByIdQuery(id, clientId), ct);
        return existing is null ? NotFound() : null;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Register([FromBody] RegisterPartnerCommand c, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct));
    }

    [HttpPost("{id:guid}/name")]
    [Authorize]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenamePartnerCommand c, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var guard = await OwnerGuardAsync(id, clientId.Value, ct);
        if (guard is not null) return guard;
        return Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    }

    [HttpPost("{id:guid}/profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdatePartnerProfileCommand c, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var guard = await OwnerGuardAsync(id, clientId.Value, ct);
        if (guard is not null) return guard;
        return Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    }

    [HttpPost("{id:guid}/remove")]
    [Authorize]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var guard = await OwnerGuardAsync(id, clientId.Value, ct);
        if (guard is not null) return guard;
        return Respond(await _mediator.Send(new RemovePartnerCommand(id), ct));
    }

    [HttpPost("{id:guid}/link-client")]
    [Authorize]
    public async Task<IActionResult> LinkClient(Guid id, [FromBody] LinkPartnerClientCommand c, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var guard = await OwnerGuardAsync(id, clientId.Value, ct);
        if (guard is not null) return guard;
        // BE-085: ClientId не из тела, а из sub.
        return Respond(await _mediator.Send(c with { AggregateId = id, ClientId = clientId.Value }, ct));
    }

    [HttpPost("{id:guid}/unlink-client")]
    [Authorize]
    public async Task<IActionResult> UnlinkClient(Guid id, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var guard = await OwnerGuardAsync(id, clientId.Value, ct);
        if (guard is not null) return guard;
        return Respond(await _mediator.Send(new UnlinkPartnerClientCommand(id), ct));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        return Ok(await _mediator.Send(new ListPartnersQuery(clientId.Value), ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var r = await _mediator.Send(new GetPartnerByIdQuery(id, clientId.Value), ct);
        return r is null ? NotFound() : Ok(r);
    }
}
