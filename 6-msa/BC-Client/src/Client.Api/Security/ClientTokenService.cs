namespace Client.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public sealed record ClientTokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public sealed class ClientTokenService
{
    private const int AccessMinutes = 15;
    private const int RefreshDays = 30;
    private readonly string _key;
    private readonly string _issuer;
    public ClientTokenService(IConfiguration config)
    { _key = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey не задан"); _issuer = config["Jwt:Issuer"] ?? "xdiary"; }

    public ClientTokenResponse Issue(Guid clientId, string phone)
    {
        var now = DateTime.UtcNow;
        return new ClientTokenResponse(
            CreateToken(clientId.ToString(), "access", now.AddMinutes(AccessMinutes), phone),
            CreateToken(clientId.ToString(), "refresh", now.AddDays(RefreshDays), phone),
            now.AddMinutes(AccessMinutes));
    }

    public bool TryValidateRefresh(string token, out Guid clientId, out string? phone)
    {
        clientId = Guid.Empty; phone = null;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _issuer,
                ValidateAudience = false, ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(_key)),
                ClockSkew = TimeSpan.Zero
            }, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var kind = principal.FindFirst("kind")?.Value;
            phone = principal.FindFirst("phone")?.Value;
            return kind == "refresh" && sub is not null && Guid.TryParse(sub, out clientId);
        }
        catch { return false; }
    }

    private string CreateToken(string sub, string kind, DateTime expires, string? phone)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_key));
        var claims = new List<Claim> { new Claim("sub", sub), new Claim("kind", kind) };
        if (!string.IsNullOrWhiteSpace(phone)) claims.Add(new Claim("phone", phone));
        var token = new JwtSecurityToken(issuer: _issuer, claims: claims, expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }
}