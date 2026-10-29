using Privacy.Application;
using Privacy.Application.Commands;
using Privacy.Application.Queries;
using Privacy.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Privacy.Api.Controllers;

/// <summary>
/// Command/Query API приватности: /api/v1/privacy.
/// </summary>
[ApiController]
[Route("api/v1/privacy")]
public class PrivacyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TokenService _tokenService;

    public PrivacyController(IMediator mediator, TokenService tokenService)
    {
        _mediator = mediator;
        _tokenService = tokenService;
    }

    /// <summary>Установить PIN (PBKDF2-SHA256, открытый PIN не сохраняется).</summary>
    [HttpPost("pin")]
    public async Task<ActionResult<PrivacyOperationResult>> SetPin(
        [FromBody] SetPinCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Удалить PIN.</summary>
    [HttpDelete("pin")]
    public async Task<ActionResult<PrivacyOperationResult>> RemovePin(
        [FromQuery] string userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemovePinCommand { UserId = userId }, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Установлен ли PIN.</summary>
    [HttpGet("has-pin")]
    public async Task<IActionResult> HasPin([FromQuery] string userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new HasPinQuery { UserId = userId }, ct);
        return Ok(result);
    }

    /// <summary>Проверить PIN.</summary>
    [HttpPost("verify-pin")]
    public async Task<IActionResult> VerifyPin([FromBody] VerifyPinQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>Выпустить access/refresh JWT (после проверки PIN).</summary>
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest request, CancellationToken ct)
    {
        var verified = await _mediator.Send(new VerifyPinQuery { UserId = request.UserId, Pin = request.Pin }, ct);
        if (!verified)
            return Unauthorized();

        var tokens = _tokenService.Issue(request.UserId);
        return Ok(tokens);
    }

    /// <summary>Проверить JWT (stateless, для шлюза).</summary>
    [HttpPost("verify-token")]
    public IActionResult VerifyToken([FromBody] VerifyTokenRequest request)
        => Ok(_tokenService.Verify(request.Token));
}

public record TokenRequest(string UserId, string Pin);
public record VerifyTokenRequest(string Token);
