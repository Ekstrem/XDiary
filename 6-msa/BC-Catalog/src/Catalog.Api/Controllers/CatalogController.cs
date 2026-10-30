namespace Catalog.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    public CatalogController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<ICatalog, ICatalogAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddCatalogItemCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameCatalogItemCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPut("{id:guid}/details")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateCatalogItemDetailsCommand c, CancellationToken ct) => Respond(await _mediator.Send(c with { AggregateId = id }, ct));
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new ArchiveCatalogItemCommand(id), ct));
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new RestoreCatalogItemCommand(id), ct));
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) => Respond(await _mediator.Send(new RemoveCatalogItemCommand(id), ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListCatalogItemsQuery(), ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new GetCatalogItemByIdQuery(id), ct); return r is null ? NotFound() : Ok(r); }
}