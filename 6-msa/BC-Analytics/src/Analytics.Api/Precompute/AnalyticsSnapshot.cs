namespace Analytics.Api.Precompute;
using Analytics.Application;
using Analytics.Domain.Models;
public sealed class AnalyticsSnapshot
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, JournalRecord> _records = new();
    private List<MetricInput> _metrics = new();
    public SummaryCards Summary30 { get; private set; } = new() { Period = "30" };
    public SummaryCards Summary90 { get; private set; } = new() { Period = "90" };
    public SummaryCards SummaryAll { get; private set; } = new() { Period = "all" };
    public List<CorrelationResult> Correlations { get; private set; } = new();
    public TopLists Tops { get; private set; } = new();
    public Distribution Distribution { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }

    public void Rebuild(IReadOnlyList<JournalRecord> records, IReadOnlyList<MetricInput> metrics)
    { lock (_lock) { _records.Clear(); foreach (var r in records) _records[r.Id] = r; _metrics = metrics.ToList(); RecomputeLocked(); } }

    public void ApplyCompleted(Guid id, JournalRecord r) { lock (_lock) { _records[id] = r; RecomputeLocked(); } }
    public void ApplyDeleted(Guid id) { lock (_lock) { if (_records.Remove(id)) RecomputeLocked(); } }
    public void ApplySleep(string date, int score) { lock (_lock) { var m = _metrics.FirstOrDefault(x => x.Date == date); if (m is null) { m = new MetricInput { Date = date, SleepScore = score }; _metrics.Add(m); } else m.SleepScore = score; RecomputeLocked(); } }

    public SummaryCards GetSummary(string period) => period switch { "30" => Summary30, "90" => Summary90, _ => SummaryAll };

    private void RecomputeLocked()
    {
        var inputs = _records.Values.Select(ToInput).ToList();
        Summary30 = SummaryCalculator.Calculate("30", inputs);
        Summary90 = SummaryCalculator.Calculate("90", inputs);
        SummaryAll = SummaryCalculator.Calculate("all", inputs);
        Correlations = CorrelationCalculator.Calculate(inputs, _metrics);
        Tops = CalcTops(_records.Values.ToList());
        Distribution = CalcDistribution(_records.Values.ToList());
        UpdatedAt = DateTime.UtcNow;
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

    private static TopLists CalcTops(List<JournalRecord> records) => new()
    {
        Positions = GroupCount(records.SelectMany(r => r.Positions)),
        Places = GroupCount(records.Where(r => !string.IsNullOrWhiteSpace(r.Place)).Select(r => r.Place)),
        Partners = GroupCount(records.SelectMany(r => r.Partners)),
        Fantasies = GroupCount(records.SelectMany(r => r.Fantasies))
    };

    private static Distribution CalcDistribution(List<JournalRecord> records)
    {
        var days = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var weekday = new int[7];
        var hours = new int[24];
        foreach (var r in records)
        {
            if (DateTime.TryParse(r.StartedAt, out var d)) { weekday[((int)d.DayOfWeek + 6) % 7]++; hours[d.Hour]++; }
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