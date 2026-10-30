using Journal.Application;
using Journal.Application.Commands;
using Journal.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Journal.Api.Controllers;

/// <summary>
/// Command/Query API журнала: /api/v1/journal.
/// Команды повторяют экраны визарда (ADR-006).
/// </summary>
[ApiController]
[Route("api/v1/journal")]
public class JournalController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalController(IMediator mediator) => _mediator = mediator;

    /// <summary>Начать ручную запись.</summary>
    [HttpPost("records")]
    public async Task<ActionResult<JournalOperationResult>> StartManualEntry(CancellationToken ct)
    {
        var result = await _mediator.Send(new StartManualEntryCommand(), ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Начать запись из занятия Garmin.</summary>
    [HttpPost("records/from-garmin")]
    public async Task<ActionResult<JournalOperationResult>> StartFromGarmin(
        [FromBody] StartFromGarminCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Указать дату.</summary>
    [HttpPost("records/{id:guid}/when")]
    public async Task<ActionResult<JournalOperationResult>> SpecifyWhen(
        Guid id, [FromBody] SpecifyWhenCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Выбрать партнёра.</summary>
    [HttpPost("records/{id:guid}/partner")]
    public async Task<ActionResult<JournalOperationResult>> ChoosePartner(
        Guid id, [FromBody] ChoosePartnerCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Указать место.</summary>
    [HttpPost("records/{id:guid}/place")]
    public async Task<ActionResult<JournalOperationResult>> SpecifyPlace(
        Guid id, [FromBody] SpecifyPlaceCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Добавить позы.</summary>
    [HttpPost("records/{id:guid}/positions")]
    public async Task<ActionResult<JournalOperationResult>> AddPositions(
        Guid id, [FromBody] AddPositionsCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Добавить аксессуары.</summary>
    [HttpPost("records/{id:guid}/accessories")]
    public async Task<ActionResult<JournalOperationResult>> AddAccessories(
        Guid id, [FromBody] AddAccessoriesCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Зафиксировать контрацепцию.</summary>
    [HttpPost("records/{id:guid}/contraception")]
    public async Task<ActionResult<JournalOperationResult>> RecordContraception(
        Guid id, [FromBody] RecordContraceptionCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Указать длительность.</summary>
    [HttpPost("records/{id:guid}/duration")]
    public async Task<ActionResult<JournalOperationResult>> SpecifyDuration(
        Guid id, [FromBody] SpecifyDurationCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Зафиксировать оргазмы (мультивыбор типа + оценка).</summary>
    [HttpPost("records/{id:guid}/orgasms")]
    public async Task<ActionResult<JournalOperationResult>> RecordOrgasms(
        Guid id, [FromBody] RecordOrgasmsCommand command, CancellationToken ct)
    {
        command.AggregateId = id;
        return await Send(command, ct);
    }

    /// <summary>Завершить запись.</summary>
    [HttpPost("records/{id:guid}/complete")]
    public async Task<ActionResult<JournalOperationResult>> Complete(Guid id, CancellationToken ct)
        => await Send(new CompleteCommand { AggregateId = id }, ct);

    /// <summary>Удалить запись.</summary>
    [HttpDelete("records/{id:guid}")]
    public async Task<ActionResult<JournalOperationResult>> Delete(Guid id, CancellationToken ct)
        => await Send(new DeleteRecordCommand { AggregateId = id }, ct);

    /// <summary>Журнал (список записей).</summary>
    [HttpGet("records")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListRecordsQuery(), ct));

    /// <summary>Деталь записи.</summary>
    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRecordByIdQuery { AggregateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    private async Task<ActionResult<JournalOperationResult>> Send<T>(T command, CancellationToken ct)
        where T : IRequest<JournalOperationResult>
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
