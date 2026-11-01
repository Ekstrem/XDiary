using Analytics.Api.Precompute;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsSnapshot _snapshot;

    public AnalyticsController(AnalyticsSnapshot snapshot) => _snapshot = snapshot;

    [HttpGet("summary")]
    public IActionResult Summary([FromQuery] string period = "30") => Ok(_snapshot.GetSummary(period));

    [HttpGet("correlations")]
    public IActionResult Correlations() => Ok(_snapshot.Correlations);

    [HttpGet("tops")]
    public IActionResult Tops() => Ok(_snapshot.Tops);

    [HttpGet("distribution")]
    public IActionResult Distribution() => Ok(_snapshot.Distribution);
    [HttpPost("events")]
    public IActionResult Events([FromBody] AnalyticsEvent e)
    {
        if (e.EventType == "RecordCompleted" && e.Record is not null) _snapshot.ApplyCompleted(e.Id, e.Record);
        else if (e.EventType == "RecordDeleted") _snapshot.ApplyDeleted(e.Id);
        else if (e.EventType == "SleepRecorded") _snapshot.ApplySleep(e.Date, e.SleepScore);
        return Ok();
    }
}