using Analytics.Api.Precompute;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsSnapshot _snapshot;

    public AnalyticsController(AnalyticsSnapshot snapshot) => _snapshot = snapshot;

    // BE-083 (P0): ключ владельца данных — sub из токена; снимок разделяется по клиенту.
    private Guid? ClientId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Summary([FromQuery] string period = "30")
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        return Ok(_snapshot.GetSummary(clientId.Value, period));
    }

    [HttpGet("correlations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Correlations()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Correlations(clientId.Value)); }

    [HttpGet("tops")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Tops()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Tops(clientId.Value)); }

    [HttpGet("distribution")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Distribution()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Distribution(clientId.Value)); }

    [HttpGet("thresholds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Thresholds()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Thresholds(clientId.Value)); }

    [HttpGet("trends")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Trends()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Trends(clientId.Value)); }

    [HttpGet("physio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Physio()
    { var clientId = ClientId(); if (clientId is null) return Unauthorized(); return Ok(_snapshot.Physio(clientId.Value)); }

    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Events([FromBody] AnalyticsEvent e)
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        if (e.EventType == "RecordCompleted" && e.Record is not null) _snapshot.ApplyCompleted(clientId.Value, e.Id, e.Record);
        else if (e.EventType == "RecordDeleted") _snapshot.ApplyDeleted(clientId.Value, e.Id);
        else if (e.EventType == "SleepRecorded") _snapshot.ApplySleep(clientId.Value, e.Date, e.SleepScore);
        else if (e.EventType == "SomaticPointRecorded") _snapshot.ApplySomatic(clientId.Value, e.T, e.Hr, e.BodyBattery);
        else if (e.EventType == "StressRecorded") _snapshot.ApplyStress(clientId.Value, e.Date, e.Stress);
        return Ok();
    }
}
