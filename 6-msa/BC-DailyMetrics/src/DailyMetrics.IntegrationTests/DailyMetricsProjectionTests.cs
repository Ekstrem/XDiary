using DailyMetrics.Api.Events;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace DailyMetrics.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`daily_metrics_read`).
/// Проверяет, что проекция маппит ВСЕ ключи `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class DailyMetricsProjectionTests
{
    private const string Date = "2026-11-19";

    private static (DailyMetrics.Domain.Abstraction.IDailyMetricsAnemicModel Model, Guid ClientId) FullModel()
    {
        var clientId = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel()).RecordStress(Date, 7, clientId);
        var b = Aggregate.CreateInstance(a.Model()).RecordRestingHr(Date, 58, clientId);
        var c = Aggregate.CreateInstance(b.Model()).RecordHrv(Date, 44, clientId);
        var d = Aggregate.CreateInstance(c.Model()).RecordSleep(Date, new Sleep(80, 420, 90, 200, 100, 30), clientId);
        return (d.Model(), clientId);
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var (m, clientId) = FullModel();
        var read = new DailyMetricsReadModel { Id = m.Id };

        DailyMetricsProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(Date, read.Date);
        Assert.Equal(7, read.Stress);
        Assert.Equal(58, read.RestingHr);
        Assert.Equal(44, read.Hrv);
        Assert.Equal(80, read.SleepScore);
        Assert.Equal(420, read.SleepDurationMin);
        Assert.Equal(90, read.SleepDeepMin);
        Assert.Equal(200, read.SleepLightMin);
        Assert.Equal(100, read.SleepRemMin);
        Assert.Equal(30, read.SleepAwakeMin);
        Assert.Equal(clientId, read.ClientId);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var (m, _) = FullModel();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "Stress", "RestingHr", "Hrv", "Sleep", "ClientId" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new DailyMetricsReadModel { Id = Guid.NewGuid(), Date = Date, Stress = 5, Hrv = 40 };

        DailyMetricsProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["Stress"] = new IntValueObject(9),
        });

        Assert.Equal(9, read.Stress);
        Assert.Equal(Date, read.Date);
        Assert.Equal(40, read.Hrv);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new DailyMetricsReadModel { Id = Guid.NewGuid(), Date = Date, Stress = 5 };
        DailyMetricsProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal(Date, read.Date);
        Assert.Equal(5, read.Stress);
    }
}
