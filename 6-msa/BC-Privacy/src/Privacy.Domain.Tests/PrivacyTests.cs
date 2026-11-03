using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.Domain.Specifications;
using Xunit;

/// <summary>TE-005: privacy — домен PIN (формат, хранение хеша, lock/unlock).</summary>
public class PrivacyTests
{
    [Fact]
    public void SetPin_StoresHashOnly_NotPlaintext()
    {
        var salt = "salt";
        var hash = "hashed";
        var iter = 100_000;
        var r = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), salt, hash, iter);

        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Equal(hash, r.Model().Root.Hash);
        Assert.Equal(salt, r.Model().Root.Salt);
        Assert.DoesNotContain("1234", r.Model().Root.Hash);
    }

    [Fact]
    public void SetPin_EmptyHash_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), "salt", "", 100_000);
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("12345678", true)]
    [InlineData("123", false)]
    [InlineData("123456789", false)]
    [InlineData("12ab", false)]
    public void PinFormatValidator_Validated(string pin, bool expected)
        => Assert.Equal(expected, new PinFormatValidator().IsSatisfiedBy(pin));

    [Fact]
    public void Lock_Unlock_Toggles()
    {
        var locked = Aggregate.CreateInstance(new AnemicModel()).Lock().Model();
        Assert.True(locked.Locked);

        var unlocked = Aggregate.CreateInstance(locked).Unlock().Model();
        Assert.False(unlocked.Locked);
    }
}
