using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.Storage;
using Xunit;

public class DailyMetricsIntegrationTest
{
    [Fact]
    public void Command_Then_Query()
    {
        var stress = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-10-29", 50);
        Assert.Equal(DomainOperationResultEnum.Success, stress.Result);
        var sleep = Aggregate.CreateInstance(stress.Model()).RecordSleep("2026-10-29", Sleep.CreateInstance(80, 480, 120, 240, 90, 30));
        Assert.Equal(DomainOperationResultEnum.Success, sleep.Result);
        var entity = new DailyMetricsEntity { Id = sleep.Model().Id };
        DailyMetricsMapper.MapToEntity(sleep.Model(), entity);
        var readModel = DailyMetricsMapper.MapToReadModel(entity);
        Assert.Equal("2026-10-29", readModel.Date);
        Assert.Equal(50, readModel.Stress);
        Assert.Equal(80, readModel.SleepScore);
    }
}