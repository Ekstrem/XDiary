namespace Privacy.Application.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Privacy.Application.Commands;

public sealed class TokenService
{
    private const int AccessMinutes = 15;
    private const int RefreshDays = 30;
    private readonly string _key;
    private readonly string _issuer;

    public TokenService(string signingKey, string issuer)
    {
        _key = string.IsNullOrWhiteSpace(signingKey) ? throw new InvalidOperationException("Jwt:SigningKey не задан") : signingKey;
        _issuer = string.IsNullOrWhiteSpace(issuer) ? "xdiary" : issuer;
    }

    public TokenResponse Issue()
    {
        var now = DateTime.UtcNow;
        return new TokenResponse(CreateToken("access", now.AddMinutes(AccessMinutes)), CreateToken("refresh", now.AddDays(RefreshDays)));
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

    private string CreateToken(string kind, DateTime expires)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_key));
        var token = new JwtSecurityToken(
            issuer: _issuer,
            claims: new[] { new Claim("kind", kind) },
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }
}