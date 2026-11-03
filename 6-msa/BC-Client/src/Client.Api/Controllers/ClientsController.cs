namespace Client.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Client.Api.Security;
using Client.Application.Commands;
using Client.Application.Queries;
using Client.Domain;
using Client.Domain.Abstraction;
using XDiary.Contracts;

public sealed record RefreshClientTokenCommand(string RefreshToken);

[ApiController]
[Route("api/v1/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ClientTokenService _tokens;
    public ClientsController(IMediator mediator, ClientTokenService tokens) { _mediator = mediator; _tokens = tokens; }
    private IActionResult Respond(AggregateResult<IClients, IClientAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterClientCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("{id:guid}/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyClientOtpCommand c, CancellationToken ct)
    {
        var result = await _mediator.Send(c with { AggregateId = id }, ct);
        if (!result.IsSuccess()) return BadRequest(result.ErrorMessage());
        var m = result.Model();
        var pair = _tokens.Issue(m.Id, m.Root.Phone);
        return Ok(new { aggregateId = m.Id, version = m.Version, result = "Verified", accessToken = pair.AccessToken, refreshToken = pair.RefreshToken, expiresAt = pair.ExpiresAt });
    }
    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult Refresh([FromBody] RefreshClientTokenCommand c)
    {
        if (!_tokens.TryValidateRefresh(c.RefreshToken, out var clientId, out var phone))
            return Unauthorized();
        var pair = _tokens.Issue(clientId, phone ?? string.Empty);
        return Ok(new { accessToken = pair.AccessToken, refreshToken = pair.RefreshToken, expiresAt = pair.ExpiresAt });
    }
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListClientsQuery(), ct));
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (sub is null || !Guid.TryParse(sub, out var id)) return Unauthorized();
        var r = await _mediator.Send(new GetClientByIdQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new GetClientByIdQuery(id), ct); return r is null ? NotFound() : Ok(r); }
}