namespace Client.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public sealed class ClientTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    public ClientTokenService(IConfiguration config)
    { _key = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey не задан"); _issuer = config["Jwt:Issuer"] ?? "xdiary"; }
    public string Issue(Guid clientId, string phone)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_key));
        var token = new JwtSecurityToken(
            issuer: _issuer,
            claims: new[] { new Claim("sub", clientId.ToString()), new Claim("phone", phone) },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }
}