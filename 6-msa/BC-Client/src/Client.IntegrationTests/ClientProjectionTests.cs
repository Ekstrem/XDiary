using Client.Api.Events;
using Client.Domain.Implementation;
using Client.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace Client.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`client_read`).
/// Проверяет, что проекция маппит все поля read-модели (класс BE-134/137/138).
/// OTP-поля (OtpCode/OtpExpiresAt/OtpAttempts) в read-модель намеренно не попадают.
/// </summary>
public class ClientProjectionTests
{
    private const string Phone = "+79000000000";

    private static (Client.Domain.Abstraction.IClientAnemicModel Model, string DisplayName) FullModel()
    {
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register(Phone, "123456");
        var ver = Aggregate.CreateInstance(reg.Model()).VerifyOtp("123456");
        var upd = Aggregate.CreateInstance(ver.Model()).UpdateProfile("Маша", "female", "http://photo");
        return (upd.Model(), "Маша");
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldOfReadModel()
    {
        var (m, displayName) = FullModel();
        var read = new ClientReadModel { Id = m.Id };

        ClientProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(Phone, read.Phone);
        Assert.Equal(displayName, read.DisplayName);
        Assert.Equal("female", read.Gender);
        Assert.Equal("http://photo", read.PhotoUrl);
        Assert.True(read.Verified);
    }

    [Fact]
    public void ApplyVos_CoversReadModelFieldsFromVoKeys()
    {
        var (m, _) = FullModel();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        // Все поля read-модели обеспечены VO-ключами (OTP-ключи в read-модель не входят).
        Assert.Subset(keys, new HashSet<string> { "Root", "DisplayName", "Gender", "PhotoUrl", "OtpCode", "OtpExpiresAt", "OtpAttempts", "Verified" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new ClientReadModel { Id = Guid.NewGuid(), Phone = Phone, DisplayName = "Старое", Gender = "female" };

        ClientProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["DisplayName"] = new StringValueObject("Новое"),
        });

        Assert.Equal("Новое", read.DisplayName);
        Assert.Equal(Phone, read.Phone);
        Assert.Equal("female", read.Gender);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new ClientReadModel { Id = Guid.NewGuid(), Phone = Phone };
        ClientProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal(Phone, read.Phone);
    }
}
