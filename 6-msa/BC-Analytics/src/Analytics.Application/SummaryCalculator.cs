using Analytics.Domain.Models;

namespace Analytics.Application;

/// <summary>
/// Сводка за период (30/90/all).
/// </summary>
public static class SummaryCalculator
{
    public static SummaryCards Calculate(string period, IReadOnlyList<RecordInput> records)
    {
        var filtered = FilterByPeriod(period, records);

        var sessions = filtered.Count;
        var totalMinutes = filtered.Sum(r => r.DurationMin);
        var avgDuration = sessions > 0 ? totalMinutes / sessions : 0;
        var orgasms = filtered.Sum(r => r.OrgasmsMe + r.OrgasmsPartner);

        var ratings = filtered
            .SelectMany(r => new[] { r.OrgasmMeRating, r.OrgasmPartnerRating })
            .Where(x => x > 0)
            .ToList();
        var avgRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0;

        return new SummaryCards
        {
            Period = period,
            Sessions = sessions,
            TotalMinutes = totalMinutes,
            AvgDuration = avgDuration,
            Orgasms = orgasms,
            AvgRating = avgRating
        };
    }

    private static List<RecordInput> FilterByPeriod(string period, IReadOnlyList<RecordInput> records)
    {
        if (period is "30" or "90" && int.TryParse(period, out var days))
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            return records
                .Where(r => DateTime.TryParse(r.StartedAt, out var d) && d >= cutoff)
                .ToList();
        }

        return records.ToList(); // "all"
    }
}
