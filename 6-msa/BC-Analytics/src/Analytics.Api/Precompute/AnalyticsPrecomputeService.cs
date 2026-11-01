namespace Analytics.Api.Precompute;
using System.Net.Http.Json;
using Analytics.Application;
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

        var inputs = records.Select(ToInput).ToList();
        var metricInputs = metrics.Select(m => new MetricInput { Date = m.Date, RestingHr = m.RestingHr, SleepScore = m.SleepScore }).ToList();

        _snapshot.Update(
            SummaryCalculator.Calculate("30", inputs),
            SummaryCalculator.Calculate("90", inputs),
            SummaryCalculator.Calculate("all", inputs),
            CorrelationCalculator.Calculate(inputs, metricInputs),
            CalculateTops(records),
            CalculateDistribution(records));
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

    private static TopLists CalculateTops(List<JournalRecord> records) => new()
    {
        Positions = GroupCount(records.SelectMany(r => r.Positions)),
        Places = GroupCount(records.Where(r => !string.IsNullOrWhiteSpace(r.Place)).Select(r => r.Place)),
        Partners = GroupCount(records.SelectMany(r => r.Partners)),
        Fantasies = GroupCount(records.SelectMany(r => r.Fantasies))
    };

    private static Distribution CalculateDistribution(List<JournalRecord> records)
    {
        var days = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var weekday = new int[7];
        var hours = new int[24];
        foreach (var r in records)
        {
            if (DateTime.TryParse(r.StartedAt, out var d))
            {
                weekday[((int)d.DayOfWeek + 6) % 7]++;
                hours[d.Hour]++;
            }
        }
        return new Distribution
        {
            Weekdays = days.Select((name, i) => new CountEntry { Key = name, Count = weekday[i] }).ToList(),
            Hours = hours.Select((c, h) => new CountEntry { Key = h.ToString(), Count = c }).ToList()
        };
    }

    private static List<CountEntry> GroupCount(IEnumerable<string> items)
        => items.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x).Select(g => new CountEntry { Key = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(10).ToList();
}