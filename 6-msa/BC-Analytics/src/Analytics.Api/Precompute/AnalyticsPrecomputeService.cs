namespace Analytics.Api.Precompute;
using System.Net.Http.Json;
using Analytics.Domain.Models;

public sealed class AnalyticsPrecomputeService : BackgroundService
{
    private readonly IHttpClientFactory _http;
    private readonly AnalyticsSnapshot _snapshot;
    private readonly ILogger<AnalyticsPrecomputeService> _logger;
    private readonly string _journalUrl;
    private readonly string _metricsUrl;

    public AnalyticsPrecomputeService(IHttpClientFactory http, AnalyticsSnapshot snapshot, IConfiguration config, ILogger<AnalyticsPrecomputeService> logger)
    { _http = http; _snapshot = snapshot; _logger = logger; _journalUrl = config["Services:Journal"] ?? "http://journal"; _metricsUrl = config["Services:DailyMetrics"] ?? "http://daily-metrics"; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RecalculateAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Analytics precompute failed"); }
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
        }
    }

    private async Task RecalculateAsync(CancellationToken ct)
    {
        var client = _http.CreateClient();
        var records = await client.GetFromJsonAsync<List<JournalRecord>>($"{_journalUrl}/api/v1/journal/records", ct) ?? new();
        var metrics = await client.GetFromJsonAsync<List<DailyMetricDto>>($"{_metricsUrl}/api/v1/daily-metrics/series", ct) ?? new();
        var metricInputs = metrics.Select(m => new MetricInput { Date = m.Date, RestingHr = m.RestingHr, SleepScore = m.SleepScore }).ToList();
        _snapshot.Rebuild(records, metricInputs);
    }
}