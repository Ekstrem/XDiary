namespace Analytics.Api.Precompute;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Analytics.Domain.Models;
using Microsoft.IdentityModel.Tokens;
using XDiary.Contracts;

public sealed class AnalyticsPrecomputeService : BackgroundService
{
    private readonly IHttpClientFactory _http;
    private readonly AnalyticsSnapshot _snapshot;
    private readonly ILogger<AnalyticsPrecomputeService> _logger;
    private readonly string _journalUrl;
    private readonly string _metricsUrl;
    private readonly string _signingKey;
    // BE-091 (@TL 08:02): не-2xx не должен «молчать» — лог + счётчик, а не только new().
    private static readonly Meter _meter = new("Analytics.Precompute");
    private static readonly Counter<long> _non2xx = _meter.CreateCounter<long>("precompute.non2xx", description: "non-2xx internal responses");

    public AnalyticsPrecomputeService(IHttpClientFactory http, AnalyticsSnapshot snapshot, IConfiguration config, ILogger<AnalyticsPrecomputeService> logger)
    { _http = http; _snapshot = snapshot; _logger = logger; _journalUrl = config["Services:Journal"] ?? "http://journal:8080"; _metricsUrl = config["Services:DailyMetrics"] ?? "http://daily-metrics:8080"; _signingKey = config["Jwt:SigningKey"] ?? ""; }

    // ADR-008: internal-вызовы не анонимны — сервис-токен (подписанный JWT, sub=analytics).
    private string CreateServiceToken()
    {
        var key = new SymmetricSecurityKey(JwtSigningKey.Resolve(_signingKey, false));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer: "xdiary", claims: new[] { new Claim("sub", "analytics") }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RecalculateAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Analytics precompute failed"); }
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
        }
    }

    private HttpRequestMessage WithAuth(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken());
        return req;
    }

    private async Task<List<JournalRecord>> GetRecordsAsync(HttpClient client, Guid cid, CancellationToken ct)
    {
        var resp = await client.SendAsync(WithAuth(HttpMethod.Get, $"{_journalUrl}/internal/journal/records?clientId={cid}"), ct);
        if (!resp.IsSuccessStatusCode) { _non2xx.Add(1, new KeyValuePair<string, object?>("op", "journal.records")); _logger.LogWarning("Journal records {clientId}: {status}", cid, resp.StatusCode); return new(); }
        return await resp.Content.ReadFromJsonAsync<List<JournalRecord>>(ct) ?? new();
    }

    private async Task<List<MetricInput>> GetMetricsAsync(HttpClient client, Guid cid, CancellationToken ct)
    {
        var resp = await client.SendAsync(WithAuth(HttpMethod.Get, $"{_metricsUrl}/internal/daily-metrics/series?clientId={cid}"), ct);
        if (!resp.IsSuccessStatusCode) { _non2xx.Add(1, new KeyValuePair<string, object?>("op", "metrics.series")); _logger.LogWarning("DailyMetrics series {clientId}: {status}", cid, resp.StatusCode); return new(); }
        var metrics = await resp.Content.ReadFromJsonAsync<List<DailyMetricDto>>(ct) ?? new();
        return metrics.Select(m => new MetricInput { Date = m.Date, RestingHr = m.RestingHr, SleepScore = m.SleepScore }).ToList();
    }

    private async Task RecalculateAsync(CancellationToken ct)
    {
        var client = _http.CreateClient();
        // BE-091: per-client — сначала список clientId из Journal, затем записи и метрики каждого клиента.
        var idsResp = await client.SendAsync(WithAuth(HttpMethod.Get, $"{_journalUrl}/internal/journal/client-ids"), ct);
        if (!idsResp.IsSuccessStatusCode) { _non2xx.Add(1, new KeyValuePair<string, object?>("op", "journal.client-ids")); _logger.LogWarning("Journal client-ids: {status}", idsResp.StatusCode); }
        var clientIds = idsResp.IsSuccessStatusCode ? await idsResp.Content.ReadFromJsonAsync<List<Guid>>(ct) ?? new() : new();
        foreach (var cid in clientIds)
        {
            var records = await GetRecordsAsync(client, cid, ct);
            var metricInputs = await GetMetricsAsync(client, cid, ct);
            _snapshot.Rebuild(cid, records, metricInputs);
        }
    }
}