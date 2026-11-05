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
    public IActionResult Summary([FromQuery] string period = "30")
    {
        var clientId = ClientId();
        if (clientId is null) return Unauthorized();
        return Ok(_snapshot.GetSummary(period));
    }

    [HttpGet("correlations")]
    public IActionResult Correlations() => Ok(_snapshot.Correlations);

    [HttpGet("tops")]
    public IActionResult Tops() => Ok(_snapshot.Tops);

    [HttpGet("distribution")]
    public IActionResult Distribution() => Ok(_snapshot.Distribution);
    [HttpGet("thresholds")]
    public IActionResult Thresholds() => Ok(_snapshot.Thresholds);
    [HttpGet("trends")]
    public IActionResult Trends() => Ok(_snapshot.Trends);
    [HttpGet("physio")]
    public IActionResult Physio() => Ok(_snapshot.Physio);
    [HttpPost("events")]
    public IActionResult Events([FromBody] AnalyticsEvent e)
    {
        if (e.EventType == "RecordCompleted" && e.Record is not null) _snapshot.ApplyCompleted(e.Id, e.Record);
        else if (e.EventType == "RecordDeleted") _snapshot.ApplyDeleted(e.Id);
        else if (e.EventType == "SleepRecorded") _snapshot.ApplySleep(e.Date, e.SleepScore);
        else if (e.EventType == "SomaticPointRecorded") _snapshot.ApplySomatic(e.T, e.Hr, e.BodyBattery);
        else if (e.EventType == "StressRecorded") _snapshot.ApplyStress(e.Date, e.Stress);
        return Ok();
    }
}