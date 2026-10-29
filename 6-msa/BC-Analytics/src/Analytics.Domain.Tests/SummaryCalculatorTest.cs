using Analytics.Application;
using Analytics.Domain.Models;
using Xunit;

namespace Analytics.Domain.Tests;

/// <summary>
/// Тесты сводки за период.
/// </summary>
public class SummaryCalculatorTest
{
    private static List<RecordInput> Records() => new()
    {
        new RecordInput { StartedAt = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm"), DurationMin = 30, OrgasmsMe = 1, OrgasmsPartner = 1, OrgasmMeRating = 4, OrgasmPartnerRating = 5 },
        new RecordInput { StartedAt = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm"), DurationMin = 45, OrgasmsMe = 0, OrgasmsPartner = 1, OrgasmMeRating = 0, OrgasmPartnerRating = 4 },
        new RecordInput { StartedAt = DateTime.UtcNow.AddDays(-100).ToString("yyyy-MM-ddTHH:mm"), DurationMin = 60, OrgasmsMe = 2, OrgasmsPartner = 0, OrgasmMeRating = 5, OrgasmPartnerRating = 0 }
    };

    [Fact]
    public void All_IncludesEveryRecord()
    {
        var summary = SummaryCalculator.Calculate("all", Records());

        Assert.Equal(3, summary.Sessions);
        Assert.Equal(135, summary.TotalMinutes);
        Assert.Equal(45, summary.AvgDuration);
        Assert.Equal(5, summary.Orgasms); // (1+1) + (0+1) + (2+0)
        Assert.Equal(4.5, summary.AvgRating); // (4+5+4+5)/4
    }

    [Fact]
    public void Period30_ExcludesOldRecords()
    {
        var summary = SummaryCalculator.Calculate("30", Records());

        Assert.Equal(2, summary.Sessions);
        Assert.Equal(75, summary.TotalMinutes);
    }
}
