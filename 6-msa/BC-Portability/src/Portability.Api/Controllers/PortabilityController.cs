namespace Portability.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Portability.Application.Commands;
using Portability.Application.Queries;
using Portability.Domain;
using Portability.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/portability")]
public sealed class PortabilityController : ControllerBase
{
    private readonly IMediator _mediator;
    public PortabilityController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IPortability, ICsvDocumentAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportRecordsCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("import/records")]
    public async Task<IActionResult> ImportRecords([FromBody] ImportRecordsCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("import/somatic")]
    public async Task<IActionResult> ImportSomatic([FromBody] ImportSomaticCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpGet("document")]
    public async Task<IActionResult> Document(CancellationToken ct)
    { var r = await _mediator.Send(new GetDocumentQuery(), ct); return r is null ? NotFound() : Ok(r); }
}