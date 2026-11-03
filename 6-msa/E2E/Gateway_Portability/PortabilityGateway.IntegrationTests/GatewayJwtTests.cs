using System.Security.Cryptography;
using System.Text;
using ApiGateway.Auth;
using Xunit;

namespace XDiary.Tests.PortabilityGateway;

/// <summary>TE-006: api-gateway — JWT (HS256): валидный/просроченный/невалидный; аноним → отказ.</summary>
public class GatewayJwtTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("xdiary-test-signing-key-32bytes-min!!");

    private static string B64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string MakeToken(long expUnix, byte[]? signKey = null)
    {
        var header = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = B64Url(Encoding.UTF8.GetBytes($"{{\"sub\":\"u1\",\"exp\":{expUnix}}}"));
        var unsigned = header + "." + payload;
        using var hmac = new HMACSHA256(signKey ?? Key);
        var sig = B64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned)));
        return unsigned + "." + sig;
    }

    private static long Future() => DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
    private static long Past() => DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

    [Fact]
    public void ValidToken_Verifies()
        => Assert.True(JwtVerifier.Verify(MakeToken(Future()), Key));

    [Fact]
    public void ExpiredToken_Fails()
        => Assert.False(JwtVerifier.Verify(MakeToken(Past()), Key));

    [Fact]
    public void WrongSigningKey_Fails()
        => Assert.False(JwtVerifier.Verify(MakeToken(Future(), Encoding.UTF8.GetBytes("another-signing-key-32bytes-min!!!!")), Key));

    [Fact]
    public void MalformedToken_Fails()
    {
        Assert.False(JwtVerifier.Verify("not-a-jwt", Key));
        Assert.False(JwtVerifier.Verify("a.b", Key));
        Assert.False(JwtVerifier.Verify("", Key));
    }
}
