using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain.Implementation;
using Xunit;

public class SomaticPointAggregateTest
{
    [Fact]
    public void RecordPoint_Valid_Succeeds()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-10-29T21:00", 72, 55, Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, res.Result);
    }
    [Fact]
    public void RecordPoint_BadT_Fails()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("bad", 72, 55, Guid.NewGuid());
        Assert.NotEqual(DomainOperationResultEnum.Success, res.Result);
    }
    [Fact]
    public void RecordPoint_Negative_Fails()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-10-29T21:00", -1, 55, Guid.NewGuid());
        Assert.NotEqual(DomainOperationResultEnum.Success, res.Result);
    }
}