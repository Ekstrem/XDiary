namespace Portability.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portability.Application.Commands;
using Portability.Application.Queries;
using Portability.Domain;
using Portability.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/portability")]
[Authorize]
public sealed class PortabilityController : ControllerBase
{
    private readonly IMediator _mediator;
    public PortabilityController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IPortability, ICsvDocumentAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    // BE-084(c): владелец документа — sub из токена; хранение и выдача привязаны к клиенту.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export([FromBody] ExportRecordsCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpPost("import/records")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ImportRecords([FromBody] ImportRecordsCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpPost("import/somatic")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ImportSomatic([FromBody] ImportSomaticCommand c, CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Respond(await _mediator.Send(c with { ClientId = clientId.Value }, ct)); }
    [HttpGet("document")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Document(CancellationToken ct)
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); var r = await _mediator.Send(new GetDocumentQuery(clientId.Value), ct); return r is null ? NotFound() : Ok(r); }
}
