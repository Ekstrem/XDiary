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

        // BE-033: у Calculate появился обязательный параметр somatic (hr/BB по времени).
        // Для проверки «мало точек → пусто» соматика пустая.
        Assert.Empty(CorrelationCalculator.Calculate(records, metrics, new Dictionary<string, (int Hr, int BodyBattery)>()));
    }

    // TE-034: контракт BE-033 — при n ≥ 3 калькулятор даёт пары:
    //   1) длительность ↔ пульс, 2) оргазмы ↔ пульс, 3) BB ↔ длительность,
    //   4) сон ↔ частота (только если у метрик SleepScore > 0).
    // В этом кейсе SleepScore = 0, поэтому пар три, а не две (старое ожидание было
    // устаревшим — до появления BB-пары).
    [Fact]
    public void Correlations_WithThreePoints_ReturnsThreeWithExplanation()
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

        // BE-033: somatic обязателен; даём hr и BB по тем же датам, что и записи.
        var somatic = new Dictionary<string, (int Hr, int BodyBattery)>
        {
            ["2026-10-01T21:00"] = (70, 50),
            ["2026-10-02T22:00"] = (72, 45),
            ["2026-10-03T20:00"] = (74, 40),
        };

        var result = CorrelationCalculator.Calculate(records, metrics, somatic);
        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(3, r.N));
        Assert.All(result, r => Assert.False(string.IsNullOrEmpty(r.Explanation)));
    }

    // TE-034: полный контракт BE-033 — все четыре пары. Добавляем SleepScore > 0,
    // чтобы включилась пара «сон ↔ частота» (в предыдущем кейсе её нет).
    [Fact]
    public void Correlations_WithThreePointsAndSleep_ReturnsFourWithExplanation()
    {
        var records = new List<RecordInput>
        {
            Rec("2026-10-01T21:00", 20, 1, 2, 5, 4),
            Rec("2026-10-02T22:00", 30, 0, 1, 0, 3),
            Rec("2026-10-03T20:00", 40, 2, 2, 4, 4)
        };
        var metrics = new List<MetricInput>
        {
            new() { Date = "2026-10-01", RestingHr = 55, SleepScore = 80 },
            new() { Date = "2026-10-02", RestingHr = 56, SleepScore = 70 },
            new() { Date = "2026-10-03", RestingHr = 57, SleepScore = 90 }
        };

        var somatic = new Dictionary<string, (int Hr, int BodyBattery)>
        {
            ["2026-10-01T21:00"] = (70, 50),
            ["2026-10-02T22:00"] = (72, 45),
            ["2026-10-03T20:00"] = (74, 40),
        };

        var result = CorrelationCalculator.Calculate(records, metrics, somatic);
        Assert.Equal(4, result.Count);
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
