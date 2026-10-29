using Portability.Application;
using Portability.Application.Commands;
using Portability.Application.Csv;
using Portability.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Portability.Api.Controllers;

/// <summary>
/// Command/Query API переносимости: /api/v1/portability.
/// </summary>
[ApiController]
[Route("api/v1/portability")]
public class PortabilityController : ControllerBase
{
    private readonly IMediator _mediator;

    public PortabilityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Заголовки CSV записей (Published Language).</summary>
    [HttpGet("headers")]
    public IActionResult Headers() => Ok(CsvHeaders.Record);

    /// <summary>Экспортировать записи в CSV.</summary>
    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportRequest request)
    {
        var csv = CsvSerializer.Serialize(request.Rows);
        return Ok(new { csv });
    }

    /// <summary>Импортировать CSV (распознать тип по заголовку).</summary>
    [HttpPost("import")]
    public IActionResult Import([FromBody] ImportRequest request)
    {
        var (headers, rows) = CsvSerializer.Deserialize(request.Csv);
        var recognized = CsvSerializer.HasValidHeaders(headers) ? "records" : "unknown";
        return Ok(new { type = recognized, headers, rows });
    }

    /// <summary>Сохранить CSV-документ (upsert по sourceId).</summary>
    [HttpPost("document")]
    public async Task<ActionResult<PortabilityOperationResult>> UpsertDocument(
        [FromBody] UpsertDocumentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Получить CSV-документ по типу источника.</summary>
    [HttpGet("document")]
    public async Task<IActionResult> GetDocument(
        [FromQuery] string sourceId = "records", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDocumentQuery { SourceId = sourceId }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}

public record ExportRequest(List<string[]> Rows);
public record ImportRequest(string Csv);
