using Journal.Application;
using Journal.Application.Commands;
using Journal.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Journal.Api.Controllers;

/// <summary>
/// Command/Query API журнала: /api/v1/journal.
/// </summary>
[ApiController]
[Route("api/v1/journal")]
public class JournalController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalController(IMediator mediator) => _mediator = mediator;

    /// <summary>Создать запись (визард → saveWizard).</summary>
    [HttpPost]
    public async Task<ActionResult<JournalOperationResult>> Create(
        [FromBody] CreateRecordCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Отредактировать запись.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JournalOperationResult>> Update(
        Guid id, [FromBody] UpdateRecordCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Удалить запись.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<JournalOperationResult>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteRecordCommand { AggregateId = id }, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Импортировать запись (upsert по id).</summary>
    [HttpPost("import")]
    public async Task<ActionResult<JournalOperationResult>> Import(
        [FromBody] ImportRecordCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Деталь записи.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRecordByIdQuery { AggregateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Журнал (список записей, startedAt DESC).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListRecordsQuery(), ct);
        return Ok(result);
    }
}
