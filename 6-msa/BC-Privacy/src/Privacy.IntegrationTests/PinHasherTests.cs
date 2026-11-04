using Xunit;
using Privacy.Application.Security;

/// <summary>TE-005: PBKDF2-SHA256 PIN (хеш+соль, без plaintext) — юнит PinHasher у провайдера.</summary>
public class PinHasherTests
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
}
