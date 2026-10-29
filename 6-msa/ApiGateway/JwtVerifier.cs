using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApiGateway.Auth;

/// <summary>
/// Проверка JWT (HS256) на шлюзе (stateless). Тот же signing key, что и у privacy.
/// </summary>
public static class JwtVerifier
{
    public static bool Verify(string token, byte[] signingKey)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            var unsigned = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(signingKey);
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
