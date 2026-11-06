namespace Analytics.Api.Precompute;
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

    private async Task RecalculateAsync(CancellationToken ct)
    {
        var client = _http.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"{_journalUrl}/internal/journal/records");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken());
        var resp = await client.SendAsync(req, ct);
        var records = resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<List<JournalRecord>>(ct) ?? new() : new();
        var metrics = await client.GetFromJsonAsync<List<DailyMetricDto>>($"{_metricsUrl}/api/v1/daily-metrics/series", ct) ?? new();
        var metricInputs = metrics.Select(m => new MetricInput { Date = m.Date, RestingHr = m.RestingHr, SleepScore = m.SleepScore }).ToList();
        // BE-083: снимок по клиенту — записи без владельца (ClientId=null) не попадают никому.
        foreach (var group in records.Where(r => r.ClientId is not null).GroupBy(r => r.ClientId!.Value))
            _snapshot.Rebuild(group.Key, group.ToList(), metricInputs);
    }
}