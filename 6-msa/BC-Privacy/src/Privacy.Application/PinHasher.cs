namespace Privacy.Application.Security;
using System.Security.Cryptography;
public static class PinHasher
{
    private const int Iterations = 100_000;
    public static (string Salt, string Hash, int Iter) Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(salt), Convert.ToHexString(hash), Iterations);
    }
    public static bool Verify(string pin, string salt, string expectedHash, int iterations)
    {
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)) return false;
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(expectedHash));
    }
}