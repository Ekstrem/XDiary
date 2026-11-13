using System.Text;
using Client.Api.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

public class ClientTokenServiceTests
{
    private static ClientTokenService Create()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "eGRpYXJ5LWRldi1zaWduaW5nLWtleS1taW5pbXVtLTMyLWJ5dGVzLTIwMjY=",
            ["Jwt:Issuer"] = "xdiary"
        }).Build();
        return new ClientTokenService(cfg);
    }

    [Fact]
    public void Issue_Returns_Access_And_Refresh()
    {
        var s = Create();
        var pair = s.Issue(Guid.NewGuid(), "+71234567890");
        Assert.False(string.IsNullOrWhiteSpace(pair.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(pair.RefreshToken));
        Assert.NotEqual(pair.AccessToken, pair.RefreshToken);
        Assert.True(pair.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void TryValidateRefresh_Valid_Returns_ClientId()
    {
        var s = Create();
        var clientId = Guid.NewGuid();
        var pair = s.Issue(clientId, "+71234567890");
        Assert.True(s.TryValidateRefresh(pair.RefreshToken, out var id));
        Assert.Equal(clientId, id);
    }

    [Fact]
    public void RefreshToken_HasNoPhone()
    {
        // AR-018: refresh несёт только sub + kind, PII (phone) отсутствует.
        var s = Create();
        var pair = s.Issue(Guid.NewGuid(), "+71234567890");
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(pair.RefreshToken);
        Assert.Equal(4, jwt.Claims.Count());               // {sub, kind} + стандартные iss, exp (JwtSecurityToken добавляет, не PII)
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == "sub"));
        Assert.Equal("refresh", jwt.Claims.FirstOrDefault(c => c.Type == "kind")?.Value);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "phone"));
    }

    [Fact]
    public void TokenPolicy_CanonicalValues_MatchADR()
    {
        // AR-018: канон по ADR-008/021 — access 15 мин, refresh 30 дней, device 5 мин.
        Assert.Equal(15, XDiary.Contracts.TokenPolicy.AccessMinutes);
        Assert.Equal(30, XDiary.Contracts.TokenPolicy.RefreshDays);
        Assert.Equal(5, XDiary.Contracts.TokenPolicy.DeviceAccessMinutes);
    }

    [Fact]
    public void TryValidateRefresh_AccessToken_ReturnsFalse()
    {
        var s = Create();
        var pair = s.Issue(Guid.NewGuid(), "+71234567890");
        Assert.False(s.TryValidateRefresh(pair.AccessToken, out _));
    }

    [Fact]
    public void TryValidateRefresh_Broken_ReturnsFalse()
    {
        var s = Create();
        Assert.False(s.TryValidateRefresh("not.a.jwt", out _));
        Assert.False(s.TryValidateRefresh(string.Empty, out _));
    }

    [Fact]
    public void TryValidateRefresh_WrongKey_ReturnsFalse()
    {
        var s = Create();
        var other = new ClientTokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "ZXhhbXBsZS1hbm90aGVyLXNpZ25pbmcta2V5LTMya3M=",
            ["Jwt:Issuer"] = "xdiary"
        }).Build());
        var pair = other.Issue(Guid.NewGuid(), "+71234567890");
        Assert.False(s.TryValidateRefresh(pair.RefreshToken, out _));
    }
}