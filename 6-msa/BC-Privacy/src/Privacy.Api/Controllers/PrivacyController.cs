namespace Privacy.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Privacy.Application.Commands;
using Privacy.Application.Queries;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/privacy")]
[Authorize]
public sealed class PrivacyController : ControllerBase
{
    private readonly IMediator _mediator;
    public PrivacyController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IPrivacy, IPinCredentialAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : BadRequest(r.ErrorMessage());
    private IActionResult RespondAuth(AggregateResult<IPrivacy, IPinCredentialAnemicModel> r) => r.IsSuccess() ? Ok(r.Model()) : (r.ErrorMessage().Contains("Текущий PIN") ? Unauthorized() : BadRequest(r.ErrorMessage()));
    private Guid CurrentClientId() { var sub = User.FindFirst("sub")?.Value; return sub is not null && Guid.TryParse(sub, out var id) ? id : Guid.Empty; }

    [HttpPost("pin/set")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetPin([FromBody] SetPinCommand c, CancellationToken ct) => RespondAuth(await _mediator.Send(c with { ClientId = CurrentClientId() }, ct));
    [HttpPost("pin/change")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePin([FromBody] ChangePinCommand c, CancellationToken ct) => RespondAuth(await _mediator.Send(c with { ClientId = CurrentClientId() }, ct));
    [HttpPost("pin/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove([FromBody] RemovePinCommand c, CancellationToken ct) => RespondAuth(await _mediator.Send(c with { ClientId = CurrentClientId() }, ct));
    [HttpPost("pin/lock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Lock(CancellationToken ct) => Respond(await _mediator.Send(new LockPinCommand(CurrentClientId()), ct));
    [HttpPost("pin/unlock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unlock(CancellationToken ct) => Respond(await _mediator.Send(new UnlockPinCommand(CurrentClientId()), ct));
    [HttpGet("pin/has")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HasPin(CancellationToken ct) => Ok(await _mediator.Send(new HasPinQuery(CurrentClientId()), ct));
    [HttpPost("pin/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyPin([FromBody] VerifyPinQuery q, CancellationToken ct) => Ok(await _mediator.Send(q with { ClientId = CurrentClientId() }, ct));

    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromBody] IssueTokenCommand c, CancellationToken ct)
    {
        var clientId = CurrentClientId();
        if (clientId == Guid.Empty) return Unauthorized();
        var r = await _mediator.Send(c with { ClientId = clientId }, ct);
        return r is null ? Unauthorized() : Ok(r);
    }

    [HttpPost("verify-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyToken([FromBody] VerifyTokenQuery q, CancellationToken ct) => Ok(await _mediator.Send(q, ct));
}
