using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>
/// Command/Query API каталога: /api/v1/catalog.
/// </summary>
[ApiController]
[Route("api/v1/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogController(IMediator mediator) => _mediator = mediator;

    /// <summary>Создать элемент каталога.</summary>
    [HttpPost]
    public async Task<ActionResult<CatalogOperationResult>> Create(
        [FromBody] CreateItemCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Отредактировать элемент каталога (в т.ч. архив/возврат).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CatalogOperationResult>> Update(
        Guid id, [FromBody] UpdateItemCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Удалить элемент каталога.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<CatalogOperationResult>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteItemCommand { AggregateId = id }, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Деталь элемента.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCatalogItemByIdQuery { AggregateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Список элементов каталога.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListCatalogItemsQuery(), ct);
        return Ok(result);
    }
}
