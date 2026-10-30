using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.Domain.Specifications;
using Xunit;

namespace XDiary.Tests.AnalyticsPrivacy;

/// <summary>TE-005: privacy — PBKDF2-SHA256 PIN (хеш+соль, без plaintext), формат, lock/unlock.</summary>
public class PrivacyTests
{
    [Fact]
    public void PinHasher_Hash_And_Verify_RoundTrip()
    {
        var (salt, hash, iter) = PinHasher.Hash("1234");
        Assert.Equal(100_000, iter);
        Assert.True(PinHasher.Verify("1234", salt, hash, iter));
        Assert.False(PinHasher.Verify("9999", salt, hash, iter));
    }

    [Fact]
    public void PinHasher_Salted_NotDeterministic()
    {
        var a = PinHasher.Hash("1234");
        var b = PinHasher.Hash("1234");
        Assert.NotEqual(a.Salt, b.Salt);
        Assert.NotEqual(a.Hash, b.Hash);
    }

    [Fact]
    public void SetPin_StoresHashOnly_NotPlaintext()
    {
        var (salt, hash, iter) = PinHasher.Hash("1234");
        var r = Aggregate.CreateInstance(new AnemicModel()).SetPin(salt, hash, iter);

        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Equal(hash, r.Model().Root.Hash);
        Assert.Equal(salt, r.Model().Root.Salt);
        Assert.DoesNotContain("1234", r.Model().Root.Hash);
    }

    [Fact]
    public void SetPin_EmptyHash_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).SetPin("salt", "", 100_000);
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
