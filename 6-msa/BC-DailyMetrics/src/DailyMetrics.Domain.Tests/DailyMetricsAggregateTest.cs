using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DailyMetrics.Domain.Implementation;
using Xunit;

public class DailyMetricsAggregateTest
{
    [Fact]
    public void RecordStress_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-10-29", 50); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void RecordStress_BadDate_Fails()
    { var r = Aggregate.CreateInstance(new AnemicModel()).RecordStress("bad", 50); Assert.NotEqual(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void RecordHrv_Negative_Fails()
    { var r = Aggregate.CreateInstance(new AnemicModel()).RecordHrv("2026-10-29", -1); Assert.NotEqual(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void RecordSleep_Valid_Succeeds()
    { var s = Sleep.CreateInstance(80, 480, 120, 240, 90, 30); var r = Aggregate.CreateInstance(new AnemicModel()).RecordSleep("2026-10-29", s); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
}