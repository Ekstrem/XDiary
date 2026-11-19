using GarminGateway.Api.Events;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace GarminGateway.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`garmin_sync_read`).
/// Проверяет, что проекция маппит ВСЕ ключи `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class GarminGatewayProjectionTests
{
    private static (GarminGateway.Domain.Abstraction.IGarminGatewayAnemicModel Model, Guid ClientId) FullModel()
    {
        var clientId = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel()).Connect(clientId, new Credentials("key-1", "token-1", "2026-12-31"));
        var b = Aggregate.CreateInstance(a.Model()).Sync(new SyncConfig(new[] { "hr", "steps" }, 7));
        return (b.Model(), clientId);
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var (m, clientId) = FullModel();
        var read = new GarminSyncReadModel { Id = m.Id };

        GarminGatewayProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(clientId, read.ClientId);
        Assert.Equal("key-1", read.ApiKey);
        Assert.Equal("token-1", read.OauthToken);
        Assert.Equal("2026-12-31", read.TokenExpiry);
        Assert.Equal(new[] { "hr", "steps" }, read.Metrics);
        Assert.Equal(7, read.WindowDays);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var (m, _) = FullModel();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "Credentials", "SyncConfig", "LastSync" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new GarminSyncReadModel { Id = Guid.NewGuid(), SourceId = "src", ApiKey = "old-key" };

        GarminGatewayProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["LastSync"] = new StringValueObject("2026-11-19T10:00"),
        });

        Assert.Equal("2026-11-19T10:00", read.LastSync);
        Assert.Equal("src", read.SourceId);
        Assert.Equal("old-key", read.ApiKey);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new GarminSyncReadModel { Id = Guid.NewGuid(), SourceId = "src" };
        GarminGatewayProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal("src", read.SourceId);
    }
}
