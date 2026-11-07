namespace SomaticSeries.Api.Events;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SomaticSeries.Application;

public sealed class HttpSomaticSeriesEventPublisher : ISomaticSeriesEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _analyticsUrl;
    private readonly ILogger<HttpSomaticSeriesEventPublisher> _log;
    private static readonly Meter _meter = new("SomaticSeries.Events");
    private static readonly Counter<long> _failures = _meter.CreateCounter<long>("publisher.failures", description: "не-2xx/исключение при публикации события в Analytics");
    public HttpSomaticSeriesEventPublisher(IHttpClientFactory http, IConfiguration config, ILogger<HttpSomaticSeriesEventPublisher> log)
    { _http = http; _analyticsUrl = config["Services:Analytics"] ?? "http://analytics:8080"; _log = log; }

    public async Task PublishPointAsync(string t, int hr, int bodyBattery, CancellationToken ct = default)
    {
        // @TL 09:38: write-путь обязан не глотать не-2xx — LogWarning + счётчик, а не пустой catch.
        try
        {
            var client = _http.CreateClient(); client.Timeout = TimeSpan.FromSeconds(2);
            var resp = await client.PostAsJsonAsync($"{_analyticsUrl}/api/v1/analytics/events", new { EventType = "SomaticPointRecorded", T = t, Hr = hr, BodyBattery = bodyBattery }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _failures.Add(1);
                _log.LogWarning("Analytics publish SomaticPointRecorded: HTTP {Status} (не-2xx)", (int)resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _failures.Add(1);
            _log.LogWarning(ex, "Analytics publish SomaticPointRecorded: канал недоступен (best-effort, fallback-расписание восстановит)");
        }
    }
}
