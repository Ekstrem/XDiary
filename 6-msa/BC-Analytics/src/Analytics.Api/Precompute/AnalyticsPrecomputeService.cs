namespace Analytics.Api.Precompute;
using System.Net.Http.Json;
using Analytics.Domain.Models;

public sealed class AnalyticsPrecomputeService : BackgroundService
{
    private readonly IHttpClientFactory _http;
    private readonly AnalyticsSnapshot _snapshot;
    private readonly ILogger<AnalyticsPrecomputeService> _logger;
    private readonly string _journalUrl;
    public AnalyticsPrecomputeService(IHttpClientFactory http, AnalyticsSnapshot snapshot, IConfiguration config, ILogger<AnalyticsPrecomputeService> logger)
    { _http = http; _snapshot = snapshot; _logger = logger; _journalUrl = config["Services:Journal"] ?? "http://journal"; }

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
        _snapshot.Update(records.Select(ToInput).ToList());
    }

    private static RecordInput ToInput(JournalRecord r)
    {
        var me = r.Orgasms.Where(o => o.Participant == "me").ToList();
        var partner = r.Orgasms.Where(o => o.Participant != "me").ToList();
        return new RecordInput
        {
            StartedAt = r.StartedAt,
            DurationMin = r.DurationMin,
            OrgasmsMe = me.Sum(o => o.Count),
            OrgasmsPartner = partner.Sum(o => o.Count),
            OrgasmMeRating = me.Count > 0 ? me.Max(o => o.Rating) : 0,
            OrgasmPartnerRating = partner.Count > 0 ? partner.Max(o => o.Rating) : 0
        };
    }
}