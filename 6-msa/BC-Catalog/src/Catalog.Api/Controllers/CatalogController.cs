namespace Catalog.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public sealed class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    public CatalogController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<ICatalog, ICatalogAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    // BE-084: владелец позиции — sub из токена; чтение и правки привязаны к клиенту.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    private async Task<IActionResult?> EnsureOwns(Guid id, Guid clientId, CancellationToken ct)
    {
        // BE-084/085: чужой id → 404 (не 403) — фильтр по clientId, не подтверждаем существование.
        var item = await _mediator.Send(new GetCatalogItemByIdQuery(id, clientId), ct);
        return item is null ? NotFound() : null;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Add([FromBody] AddCatalogItemCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }

    [HttpPost("{id:guid}/name")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameCatalogItemCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var guard = await EnsureOwns(id, clientId.Value, ct); if (guard is not null) return guard; return Respond(await _mediator.Send(c with { AggregateId = id }, ct)); }

    [HttpPost("{id:guid}/details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateCatalogItemDetailsCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var guard = await EnsureOwns(id, clientId.Value, ct); if (guard is not null) return guard; return Respond(await _mediator.Send(c with { AggregateId = id }, ct)); }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var guard = await EnsureOwns(id, clientId.Value, ct); if (guard is not null) return guard; return Respond(await _mediator.Send(new ArchiveCatalogItemCommand(id), ct)); }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var guard = await EnsureOwns(id, clientId.Value, ct); if (guard is not null) return guard; return Respond(await _mediator.Send(new RestoreCatalogItemCommand(id), ct)); }

    [HttpPost("{id:guid}/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var guard = await EnsureOwns(id, clientId.Value, ct); if (guard is not null) return guard; return Respond(await _mediator.Send(new RemoveCatalogItemCommand(id), ct)); }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(await _mediator.Send(new ListCatalogItemsQuery(clientId.Value), ct)); }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        var r = await _mediator.Send(new GetCatalogItemByIdQuery(id, clientId.Value), ct);
        return r is null ? NotFound() : Ok(r);
    }
}
