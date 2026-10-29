using Partners.Application;
using Partners.Application.Commands;
using Partners.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Partners.Api.Controllers;

/// <summary>
/// Command/Query API партнёров: /api/v1/partners.
/// </summary>
[ApiController]
[Route("api/v1/partners")]
public class PartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PartnersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Создать профиль партнёра.</summary>
    [HttpPost]
    public async Task<ActionResult<PartnersOperationResult>> Create(
        [FromBody] CreatePartnerCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Отредактировать профиль партнёра.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartnersOperationResult>> Update(
        Guid id, [FromBody] UpdatePartnerCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Удалить партнёра.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<PartnersOperationResult>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePartnerCommand { AggregateId = id }, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Деталь партнёра.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPartnerByIdQuery { AggregateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Список партнёров.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListPartnersQuery(), ct);
        return Ok(result);
    }
}
