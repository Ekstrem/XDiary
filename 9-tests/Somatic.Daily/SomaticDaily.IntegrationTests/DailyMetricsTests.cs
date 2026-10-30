using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.InternalContracts;
using DailyMetrics.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Xunit;

namespace XDiary.Tests.SomaticDaily;

/// <summary>TE-003: daily-metrics — дневные сводки + отсутствие метрик у источника (Command → Query → Assert).</summary>
public class DailyMetricsTests
{
    private static DailyMetricsReadModel ToRead(IDailyMetricsAnemicModel m)
    {
        var e = new DailyMetricsEntity();
        DailyMetricsMapper.MapToEntity(m, e);
        return DailyMetricsMapper.MapToReadModel(e);
    }

    [Fact]
    public void RecordStress_RestingHrv_Sleep_Query_ReturnsAll()
    {
        var m = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-10-29", 35).Model();
        m = Aggregate.CreateInstance(m).RecordRestingHr("2026-10-29", 55).Model();
        m = Aggregate.CreateInstance(m).RecordHrv("2026-10-29", 37).Model();
        m = Aggregate.CreateInstance(m).RecordSleep("2026-10-29", Sleep.CreateInstance(82, 371, 60, 214, 97, 3)).Model();

        var read = ToRead(m);
        Assert.Equal("2026-10-29", read.Date);
        Assert.Equal(35, read.Stress);
        Assert.Equal(55, read.RestingHr);
        Assert.Equal(37, read.Hrv);
        Assert.Equal(82, read.SleepScore);
        Assert.Equal(371, read.SleepDurationMin);
        Assert.Equal(60, read.SleepDeepMin);
        Assert.Equal(214, read.SleepLightMin);
        Assert.Equal(97, read.SleepRemMin);
        Assert.Equal(3, read.SleepAwakeMin);
    }

    [Theory]
    [InlineData("2026-10-29", true)]
    [InlineData("2026-1-9", false)]
    [InlineData("29.10.2026", false)]
    [InlineData("", false)]
    public void RecordStress_DateFormat_Validated(string date, bool expectedSuccess)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordStress(date, 35);
        Assert.Equal(expectedSuccess, r.Result == DomainOperationResultEnum.Success);
    }

    [Fact]
    public void RecordStress_Negative_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-10-29", -1);
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void RecordRestingHr_Zero_Succeeds()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordRestingHr("2026-10-29", 0);
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void MissingMetric_IsAbsent_NotGuessed()
    {
        // Если импортирован только стресс, остальные метрики остаются 0/пустыми (не подставляются).
        var m = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-10-29", 40).Model();
        var read = ToRead(m);
        Assert.Equal(40, read.Stress);
        Assert.Equal(0, read.RestingHr);
        Assert.Equal(0, read.Hrv);
        Assert.Equal(0, read.SleepScore);
    }
}
