using Analytics.Domain.Models;

namespace Analytics.Application;

/// <summary>
/// Корреляции (Pearson) между записями журнала и дневными метриками.
/// </summary>
public static class CorrelationCalculator
{
    public static List<CorrelationResult> Calculate(IReadOnlyList<RecordInput> records, IReadOnlyList<MetricInput> metrics)
    {
        var results = new List<CorrelationResult>();
        var metricsByDate = metrics
            .Where(m => m.RestingHr > 0)
            .GroupBy(m => m.Date)
            .ToDictionary(g => g.Key, g => g.First().RestingHr);

        var pairs = records
            .Where(r => r.StartedAt.Length >= 10)
            .Select(r => new { Date = r.StartedAt[..10], r.DurationMin, Orgasms = r.OrgasmsMe + r.OrgasmsPartner })
            .Where(r => metricsByDate.ContainsKey(r.Date))
            .Select(r => (r.DurationMin, r.Orgasms, RestingHr: metricsByDate[r.Date]))
            .ToList();

        if (pairs.Count >= 3)
        {
            var duration = pairs.Select(p => (double)p.DurationMin).ToList();
            var restingHr = pairs.Select(p => (double)p.RestingHr).ToList();
            var c = PearsonCorrelation.Compute(duration, restingHr);
            results.Add(new CorrelationResult
            {
                Label = "длительность ↔ пульс покоя",
                Coefficient = Math.Round(c, 3),
                N = pairs.Count,
                Explanation = Explain(c)
            });
        }

        if (pairs.Count >= 3)
        {
            var orgasms = pairs.Select(p => (double)p.Orgasms).ToList();
            var restingHr = pairs.Select(p => (double)p.RestingHr).ToList();
            var c = PearsonCorrelation.Compute(orgasms, restingHr);
            results.Add(new CorrelationResult
            {
                Label = "оргазмы ↔ пульс покоя",
                Coefficient = Math.Round(c, 3),
                N = pairs.Count,
                Explanation = Explain(c)
            });
        }

        return results;
    }

    private static string Explain(double c)
    {
        var strength = Math.Abs(c) switch
        {
            >= 0.7 => "сильная",
            >= 0.4 => "умеренная",
            _ => "слабая"
        };
        var direction = c >= 0 ? "положительная" : "отрицательная";
        return $"{strength} {direction} связь";
    }
}
