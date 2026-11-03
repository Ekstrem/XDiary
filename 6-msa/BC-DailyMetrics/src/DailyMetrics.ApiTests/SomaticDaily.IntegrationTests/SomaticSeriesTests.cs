using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.InternalContracts;
using SomaticSeries.Storage;
using Xunit;

namespace XDiary.Tests.SomaticDaily;

/// <summary>TE-003: somatic-series — минутная гранулярность + срез/точки (Command → Query → Assert).</summary>
public class SomaticSeriesTests
{
    private static ISomaticSeriesAnemicModel Record(string t, int hr, int bb)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordPoint(t, hr, bb);
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        return r.Model();
    }

    private static SomaticSeriesReadModel ToRead(ISomaticSeriesAnemicModel m)
    {
        var e = new SomaticPointEntity();
        SomaticSeriesMapper.MapToEntity(m, e);
        return SomaticSeriesMapper.MapToReadModel(e);
    }

    [Fact]
    public void RecordPoint_MinuteGranularity_Query_Returns()
    {
        var read = ToRead(Record("2026-10-29T21:03", 72, 85));
        Assert.Equal("2026-10-29T21:03", read.T);
        Assert.Equal(72, read.Hr);
        Assert.Equal(85, read.BodyBattery);
    }

    [Theory]
    [InlineData("2026-10-29T21:03:00")]
    [InlineData("2026-10-29 21:03")]
    [InlineData("20261029T2103")]
    [InlineData("")]
    public void RecordPoint_InvalidTimeFormat_Fails(string t)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordPoint(t, 72, 85);
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(50, -1)]
    public void RecordPoint_Negative_Fails(int hr, int bb)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-10-29T21:03", hr, bb);
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void RecordPoint_ZeroValues_Succeeds()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-10-29T21:03", 0, 0);
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }
}
