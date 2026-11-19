using SomaticSeries.Api.Events;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace SomaticSeries.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`somatic_read`).
/// Проверяет, что проекция маппит ВСЕ поля из `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class SomaticSeriesProjectionTests
{
    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var clientId = Guid.NewGuid();
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-11-19T10:00", 62, 45, clientId);
        var m = res.Model();
        var read = new SomaticSeriesReadModel { Id = m.Id };

        SomaticSeriesProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal("2026-11-19T10:00", read.T);
        Assert.Equal(62, read.Hr);
        Assert.Equal(45, read.BodyBattery);
        Assert.Equal(clientId, read.ClientId);
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new SomaticSeriesReadModel { Id = Guid.NewGuid(), T = "2026-11-19T10:00", Hr = 60, BodyBattery = 40 };

        SomaticSeriesProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["Hr"] = new IntValueObject(70),
        });

        Assert.Equal(70, read.Hr);
        Assert.Equal("2026-11-19T10:00", read.T);
        Assert.Equal(40, read.BodyBattery);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new SomaticSeriesReadModel { Id = Guid.NewGuid(), T = "2026-11-19T10:00", Hr = 60 };
        SomaticSeriesProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal("2026-11-19T10:00", read.T);
        Assert.Equal(60, read.Hr);
    }
}
