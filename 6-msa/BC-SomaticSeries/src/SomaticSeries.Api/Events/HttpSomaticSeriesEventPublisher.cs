namespace SomaticSeries.Api.Events;
using System.Net.Http.Json;
using SomaticSeries.Application;

public sealed class HttpSomaticSeriesEventPublisher : ISomaticSeriesEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _analyticsUrl;
    public HttpSomaticSeriesEventPublisher(IHttpClientFactory http, IConfiguration config)
    { _http = http; _analyticsUrl = config["Services:Analytics"] ?? "http://analytics"; }

    public async Task PublishPointAsync(string t, int hr, int bodyBattery, CancellationToken ct = default)
    {
        try { var client = _http.CreateClient(); await client.PostAsJsonAsync($"{_analyticsUrl}/api/v1/analytics/events", new { EventType = "SomaticPointRecorded", T = t, Hr = hr, BodyBattery = bodyBattery }, ct); }
        catch { }
    }
}