using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>
/// BFF-агрегация (ADR-007): составные экраны собирают данные из нескольких сервисов.
/// BE-141: [Authorize] обязателен — защита не должна зависеть только от кастомного
/// middleware шлюза (список анонимных путей), иначе без токена получаем 500 с текстом ошибки.
/// </summary>
[ApiController]
[Authorize]
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
        // BE-141: токен шлюза пробрасываем до сервисов — иначе они отвечают 401 и мы отдаём 500.
        var auth = Request.Headers.Authorization.ToString();

        try
        {
            var journal = await GetStringAsync(client, $"{journalBase}/api/v1/journal", auth, ct);
            var metrics = await GetStringAsync(client,
                $"{dailyMetricsBase}/api/v1/daily-metrics/series?fromDate=1970-01-01&toDate=2999-12-31", auth, ct);

            return Ok(new { period, journal, metrics });
        }
        catch (HttpRequestException)
        {
            // Никакого текста внутренней ошибки наружу — только факт недоступности агрегата.
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "analytics upstream unavailable" });
        }
    }

    private static async Task<string> GetStringAsync(
        HttpClient client, string url, string authorization, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
