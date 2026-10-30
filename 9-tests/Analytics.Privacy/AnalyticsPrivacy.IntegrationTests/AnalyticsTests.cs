using Analytics.Application;
using Analytics.Domain.Models;
using Xunit;

namespace XDiary.Tests.AnalyticsPrivacy;

/// <summary>TE-005: analytics — read-only сводка + корреляции (n ≥ 3), не трогает домен/Event Store.</summary>
public class AnalyticsTests
{
    private static RecordInput Rec(string startedAt, int dur, int om, int op, int rm, int rp)
        => new() { StartedAt = startedAt, DurationMin = dur, OrgasmsMe = om, OrgasmsPartner = op, OrgasmMeRating = rm, OrgasmPartnerRating = rp };

    [Fact]
    public void Summary_All_ComputesTotals()
    {
        var records = new List<RecordInput>
        {
            Rec("2026-10-01T21:00", 20, 1, 2, 5, 4),
            Rec("2026-10-02T22:00", 30, 0, 1, 0, 3)
        };

        var s = SummaryCalculator.Calculate("all", records);
        Assert.Equal(2, s.Sessions);
        Assert.Equal(50, s.TotalMinutes);
        Assert.Equal(25, s.AvgDuration);
        Assert.Equal(4, s.Orgasms);
        Assert.Equal(4.0, s.AvgRating);
    }

    [Fact]
    public void Summary_Empty_ReturnsZeros()
    {
        var s = SummaryCalculator.Calculate("all", new List<RecordInput>());
        Assert.Equal(0, s.Sessions);
        Assert.Equal(0, s.AvgRating);
    }

    [Fact]
    public void Correlations_RequireAtLeast3_OtherwiseEmpty()
    {
        var records = new List<RecordInput>
        {
            Rec("2026-10-01T21:00", 20, 1, 2, 5, 4),
            Rec("2026-10-02T22:00", 30, 0, 1, 0, 3)
        };
        var metrics = new List<MetricInput>
        {
            new() { Date = "2026-10-01", RestingHr = 55 },
            new() { Date = "2026-10-02", RestingHr = 56 }
        };

        Assert.Empty(CorrelationCalculator.Calculate(records, metrics));
    }

    [Fact]
    public void Correlations_WithThreePoints_ReturnsTwoWithExplanation()
    {
        var records = new List<RecordInput>
        {
            Rec("2026-10-01T21:00", 20, 1, 2, 5, 4),
            Rec("2026-10-02T22:00", 30, 0, 1, 0, 3),
            Rec("2026-10-03T20:00", 40, 2, 2, 4, 4)
        };
        var metrics = new List<MetricInput>
        {
            new() { Date = "2026-10-01", RestingHr = 55 },
            new() { Date = "2026-10-02", RestingHr = 56 },
            new() { Date = "2026-10-03", RestingHr = 57 }
        };

        var result = CorrelationCalculator.Calculate(records, metrics);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(3, r.N));
        Assert.All(result, r => Assert.False(string.IsNullOrEmpty(r.Explanation)));
    }

    [Fact]
    public void Pearson_PerfectPositive_ReturnsOne()
        => Assert.Equal(1.0, PearsonCorrelation.Compute(new[] { 1.0, 2, 3 }, new[] { 2.0, 4, 6 }), 3);

    [Fact]
    public void Pearson_ConstantSeries_ReturnsZero()
        => Assert.Equal(0, PearsonCorrelation.Compute(new[] { 5.0, 5, 5 }, new[] { 1.0, 2, 3 }));
}
