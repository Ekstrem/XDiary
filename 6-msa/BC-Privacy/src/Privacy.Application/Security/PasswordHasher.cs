using System.Security.Cryptography;

namespace Privacy.Application.Security;

/// <summary>
/// PBKDF2-SHA256 хеширование PIN. Открытый PIN никогда не сохраняется.
/// </summary>
public static class PasswordHasher
{
    public const int DefaultIterations = 100000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string pin, string saltBase64, int iterations)
    {
        var salt = Convert.FromBase64String(saltBase64);
        using var pbkdf2 = new Rfc2898DeriveBytes(pin, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);
        return Convert.ToHexString(hash);
    }

    public static bool Verify(string pin, string saltBase64, string expectedHashHex, int iterations)
    {
        var actual = Hash(pin, saltBase64, iterations);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actual),
            Convert.FromHexString(expectedHashHex));
    }
}
