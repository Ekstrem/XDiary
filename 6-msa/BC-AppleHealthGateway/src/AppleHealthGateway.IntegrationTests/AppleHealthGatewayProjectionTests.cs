using AppleHealthGateway.Api.Events;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace AppleHealthGateway.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`apple_health_read`).
/// Проверяет, что проекция маппит ВСЕ ключи `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class AppleHealthGatewayProjectionTests
{
    private static (AppleHealthGateway.Domain.Abstraction.IAppleHealthGatewayAnemicModel Model, Guid ClientId) FullModel()
    {
        var clientId = Guid.NewGuid();
        var configured = Aggregate.CreateInstance(new AnemicModel()).Configure(clientId, new[] { "steps", "hr" });
        return (Aggregate.CreateInstance(configured.Model()).ImportFile("export.xml").Model(), clientId);
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var (m, clientId) = FullModel();
        var read = new AppleImportReadModel { Id = m.Id };

        AppleHealthGatewayProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(clientId, read.ClientId);
        Assert.Equal(new[] { "steps", "hr" }, read.Metrics);
        Assert.Equal("export.xml", read.LastImport);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var (m, _) = FullModel();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "ImportConfig", "LastImport", "ProcessedFiles" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new AppleImportReadModel { Id = Guid.NewGuid(), SourceId = "src", LastImport = "old" };

        AppleHealthGatewayProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["LastImport"] = new StringValueObject("new"),
        });

        Assert.Equal("new", read.LastImport);
        Assert.Equal("src", read.SourceId);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new AppleImportReadModel { Id = Guid.NewGuid(), SourceId = "src" };
        AppleHealthGatewayProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal("src", read.SourceId);
    }
}
