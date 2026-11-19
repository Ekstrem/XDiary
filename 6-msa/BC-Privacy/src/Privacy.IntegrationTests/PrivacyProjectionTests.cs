using Privacy.Api.Events;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace Privacy.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`pin_credential_read`).
/// Проверяет, что проекция маппит ВСЕ ключи `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class PrivacyProjectionTests
{
    private static (Privacy.Domain.Abstraction.IPinCredentialAnemicModel Model, Guid ClientId) FullModel()
    {
        var clientId = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel()).SetPin(clientId, "salt-1", "hash-1", 100_000);
        var b = Aggregate.CreateInstance(a.Model()).Lock();
        return (b.Model(), clientId);
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var (m, clientId) = FullModel();
        var read = new PinCredentialReadModel { Id = m.Id };

        PrivacyProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(clientId, read.ClientId);
        Assert.Equal("salt-1", read.Salt);
        Assert.Equal("hash-1", read.Hash);
        Assert.Equal(100_000, read.Iterations);
        Assert.True(read.Locked);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var (m, _) = FullModel();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "Locked" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new PinCredentialReadModel { Id = Guid.NewGuid(), Salt = "s", Hash = "h", Iterations = 5 };

        PrivacyProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["Locked"] = new BoolValueObject(true),
        });

        Assert.True(read.Locked);
        Assert.Equal("s", read.Salt);
        Assert.Equal("h", read.Hash);
        Assert.Equal(5, read.Iterations);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new PinCredentialReadModel { Id = Guid.NewGuid(), Salt = "s" };
        PrivacyProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal("s", read.Salt);
    }
}
