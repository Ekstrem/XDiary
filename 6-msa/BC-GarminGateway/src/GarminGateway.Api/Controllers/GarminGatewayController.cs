namespace GarminGateway.Api.Controllers;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Api.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using GarminGateway.Application.Commands;
using GarminGateway.Application.Queries;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using XDiary.Contracts;

[ApiController]
[Route("api/v1/garmin")]
public sealed class GarminGatewayController : ControllerBase
{
    private readonly IMediator _mediator;
    public GarminGatewayController(IMediator mediator) => _mediator = mediator;
    private IActionResult Respond(AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> r)
    {
        if (r.IsSuccess()) return Ok(GarminConnectionState.FromAnemicModel(r.Model()));
        return r.ErrorMessage() switch
        {
            "not_connected" => BadRequest(new GarminApiError("not_connected", "Сначала подключите Garmin в настройках.")),
            "token_expired" => BadRequest(new GarminApiError("token_expired", "Срок действия токена Garmin истёк. Вставьте новый токен.")),
            "upstream_unavailable" => StatusCode(StatusCodes.Status502BadGateway,
                new GarminApiError("upstream_unavailable", "Garmin временно недоступен. Повторите позже.")),
            _ => BadRequest(new GarminApiError("validation_error", "Проверьте apiKey, oauthToken, tokenExpiry и параметры синхронизации.")),
        };
    }
    private Guid CurrentClientId() { var sub = User.FindFirst("sub")?.Value; return sub is not null && Guid.TryParse(sub, out var id) ? id : Guid.Empty; }

    [HttpPost("connect")]
    [ProducesResponseType(typeof(GarminConnectionState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Connect([FromBody] ConnectCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("sync")]
    [ProducesResponseType(typeof(GarminConnectionState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Sync([FromBody] SyncCommand c, CancellationToken ct) => Respond(await _mediator.Send(c, ct));
    [HttpPost("fetch-activities")]
    [ProducesResponseType(typeof(GarminConnectionState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GarminApiError), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> FetchActivities(CancellationToken ct) => Respond(await _mediator.Send(new FetchActivitiesCommand(), ct));
    [HttpGet("state")]
    [ProducesResponseType(typeof(GarminConnectionState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> State(CancellationToken ct)
    { var r = await _mediator.Send(new GetSyncStateQuery(CurrentClientId()), ct); return r is null ? NotFound() : Ok(GarminConnectionState.FromReadModel(r)); }
}
