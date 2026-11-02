using Analytics.Domain.Models;

namespace Analytics.Application;

public static class CorrelationCalculator
{
    public static List<CorrelationResult> Calculate(
        IReadOnlyList<RecordInput> records,
        IReadOnlyList<MetricInput> metrics,
        IReadOnlyDictionary<string, (int Hr, int BodyBattery)> somatic)
    {
        var results = new List<CorrelationResult>();

        var pts = new List<(DateTime T, int Hr, int BodyBattery)>();
        foreach (var kv in somatic) { if (DateTime.TryParse(kv.Key, out var t)) pts.Add((t, kv.Value.Hr, kv.Value.BodyBattery)); }
        pts.Sort((a, b) => a.T.CompareTo(b.T));

        var sessionPairs = new List<(int Duration, int Orgasms, double AvgHr, int BbDelta)>();
        foreach (var r in records)
        {
            if (!DateTime.TryParse(r.StartedAt, out var start)) continue;
            var window = pts.Where(p => p.T >= start.AddMinutes(-30) && p.T <= start.AddMinutes(30)).ToList();
            if (window.Count == 0) continue;
            var avgHr = window.Average(p => p.Hr);
            var bbDelta = window.Max(p => p.BodyBattery) - window.Min(p => p.BodyBattery);
            sessionPairs.Add((r.DurationMin, r.OrgasmsMe + r.OrgasmsPartner, avgHr, bbDelta));
        }

        if (sessionPairs.Count >= 3)
            results.Add(Make("длительность ↔ пульс (hr)", PearsonCorrelation.Compute(sessionPairs.Select(p => (double)p.Duration).ToList(), sessionPairs.Select(p => p.AvgHr).ToList()), sessionPairs.Count));
        if (sessionPairs.Count >= 3)
            results.Add(Make("оргазмы ↔ пульс (hr)", PearsonCorrelation.Compute(sessionPairs.Select(p => (double)p.Orgasms).ToList(), sessionPairs.Select(p => p.AvgHr).ToList()), sessionPairs.Count));
        if (sessionPairs.Count >= 3)
            results.Add(Make("BB ↔ длительность", PearsonCorrelation.Compute(sessionPairs.Select(p => (double)p.BbDelta).ToList(), sessionPairs.Select(p => (double)p.Duration).ToList()), sessionPairs.Count));

        var sleepByDate = metrics.Where(m => m.SleepScore > 0).GroupBy(m => m.Date).ToDictionary(g => g.Key, g => g.First().SleepScore);
        var freqByDate = records.Where(r => r.StartedAt.Length >= 10).GroupBy(r => r.StartedAt[..10]).ToDictionary(g => g.Key, g => g.Count());
        var sleepPairs = sleepByDate.Keys.Where(d => freqByDate.ContainsKey(d)).Select(d => (Sleep: (double)sleepByDate[d], Freq: (double)freqByDate[d])).ToList();
        if (sleepPairs.Count >= 3)
            results.Add(Make("сон ↔ частота", PearsonCorrelation.Compute(sleepPairs.Select(p => p.Sleep).ToList(), sleepPairs.Select(p => p.Freq).ToList()), sleepPairs.Count));

        return results;
    }

    private static CorrelationResult Make(string label, double c, int n) => new CorrelationResult { Label = label, Coefficient = Math.Round(c, 3), N = n, Explanation = Explain(c) };

    private static string Explain(double c)
    {
        var strength = Math.Abs(c) switch { >= 0.7 => "сильная", >= 0.4 => "умеренная", _ => "слабая" };
        var direction = c >= 0 ? "положительная" : "отрицательная";
        return $"{strength} {direction} связь";
    }
}