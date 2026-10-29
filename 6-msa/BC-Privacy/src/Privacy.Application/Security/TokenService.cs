using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Privacy.Application.Security;

/// <summary>
/// Выпуск/проверка JWT (HS256) для шлюза. Краткоживущий access + refresh.
/// </summary>
public class TokenService
{
    public const string Issuer = "xdiary";
    private readonly byte[] _signingKey;

    public TokenService(string signingKeyBase64)
    {
        _signingKey = Convert.FromBase64String(signingKeyBase64);
    }

    public TokenResult Issue(string userId)
    {
        var access = CreateToken(userId, "access", TimeSpan.FromMinutes(15));
        var refresh = CreateToken(userId, "refresh", TimeSpan.FromDays(30));
        return new TokenResult(access, refresh);
    }

    public bool Verify(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            var unsigned = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_signingKey);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned));
            if (Base64Url(sig) != parts[2])
                return false;

            var payload = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(parts[1]));
            if (!payload.TryGetProperty("exp", out var expElement))
                return false;
            var exp = expElement.GetInt64();
            return exp > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch
        {
            return false;
        }
    }

    private string CreateToken(string userId, string scope, TimeSpan lifetime)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" })));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            jti = Guid.NewGuid().ToString(),
            sub = userId,
            scope,
            iss = Issuer,
            exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds()
        })));
        var unsigned = $"{header}.{payload}";
        using var hmac = new HMACSHA256(_signingKey);
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned));
        return $"{unsigned}.{Base64Url(sig)}";
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}

/// <summary>Результат выпуска токенов.</summary>
public record TokenResult(string AccessToken, string RefreshToken);
