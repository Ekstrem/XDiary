namespace Privacy.Application.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Privacy.Application.Commands;

public sealed class TokenService
{
    private const int AccessMinutes = 15;
    private const int DeviceAccessMinutes = 5;
    private const int RefreshDays = 30;
    private readonly string _key;
    private readonly string _issuer;

    public TokenService(string signingKey, string issuer)
    {
        _key = string.IsNullOrWhiteSpace(signingKey) ? throw new InvalidOperationException("Jwt:SigningKey не задан") : signingKey;
        _issuer = string.IsNullOrWhiteSpace(issuer) ? "xdiary" : issuer;
    }

    // После валидного PIN (sub="pin")
    public TokenResponse Issue()
    {
        var now = DateTime.UtcNow;
        return new TokenResponse(CreateToken("pin", "access", now.AddMinutes(AccessMinutes)), CreateToken("pin", "refresh", now.AddDays(RefreshDays)));
    }

    // Без PIN — анонимный device-токен (sub="device", короткий TTL)
    public TokenResponse IssueDevice()
    {
        var now = DateTime.UtcNow;
        return new TokenResponse(CreateToken("device", "access", now.AddMinutes(DeviceAccessMinutes)), CreateToken("device", "refresh", now.AddDays(RefreshDays)));
    }

    public bool Verify(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(_key)),
                ClockSkew = TimeSpan.Zero
            }, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string CreateToken(string sub, string kind, DateTime expires)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_key));
        var token = new JwtSecurityToken(
            issuer: _issuer,
            claims: new[] { new Claim("sub", sub), new Claim("kind", kind) },
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }
}