using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>
/// BFF-агрегация (ADR-007): составные экраны собирают данные из нескольких сервисов.
/// </summary>
[ApiController]
[Route("api/v1/bff")]
public class BffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BffController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>Сводка аналитики: данные из journal + daily-metrics.</summary>
    [HttpGet("analytics-summary")]
    public async Task<IActionResult> AnalyticsSummary(
        [FromQuery] string period = "30", CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();

        var journalBase = _configuration["Services:Journal"] ?? "http://journal:8080";
        var dailyMetricsBase = _configuration["Services:DailyMetrics"] ?? "http://daily-metrics:8080";

        var journal = await client.GetStringAsync($"{journalBase}/api/v1/journal", ct);
        var metrics = await client.GetStringAsync(
            $"{dailyMetricsBase}/api/v1/daily-metrics/series?fromDate=1970-01-01&toDate=2999-12-31", ct);

        return Ok(new { period, journal, metrics });
    }
}
