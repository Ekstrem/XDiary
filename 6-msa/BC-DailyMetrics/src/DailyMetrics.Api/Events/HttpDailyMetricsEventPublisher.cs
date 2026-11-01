namespace DailyMetrics.Api.Events;
using System.Net.Http.Json;
using DailyMetrics.Application;

public sealed class HttpDailyMetricsEventPublisher : IDailyMetricsEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _analyticsUrl;
    public HttpDailyMetricsEventPublisher(IHttpClientFactory http, IConfiguration config)
    { _http = http; _analyticsUrl = config["Services:Analytics"] ?? "http://analytics"; }

    public Task PublishStressAsync(string date, int stress, CancellationToken ct = default)
        => PostAsync(new { EventType = "StressRecorded", Date = date, Stress = stress }, ct);
    public Task PublishSleepAsync(string date, int score, CancellationToken ct = default)
        => PostAsync(new { EventType = "SleepRecorded", Date = date, SleepScore = score }, ct);

    private async Task PostAsync(object payload, CancellationToken ct)
    {
        try { var client = _http.CreateClient(); await client.PostAsJsonAsync($"{_analyticsUrl}/api/v1/analytics/events", payload, ct); }
        catch { }
    }
}