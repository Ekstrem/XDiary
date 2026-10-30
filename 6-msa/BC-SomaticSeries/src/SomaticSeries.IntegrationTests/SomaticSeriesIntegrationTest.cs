using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.Storage;
using Xunit;

public class SomaticSeriesIntegrationTest
{
    [Fact]
    public void Command_Then_Query()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-10-29T21:00", 72, 55);
        Assert.Equal(DomainOperationResultEnum.Success, res.Result);
        var entity = new SomaticPointEntity { Id = res.Model().Id };
        SomaticSeriesMapper.MapToEntity(res.Model(), entity);
        var readModel = SomaticSeriesMapper.MapToReadModel(entity);
        Assert.Equal("2026-10-29T21:00", readModel.T);
        Assert.Equal(72, readModel.Hr);
    }
}