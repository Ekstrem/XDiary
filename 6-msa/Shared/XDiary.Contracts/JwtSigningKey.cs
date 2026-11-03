using System;

namespace XDiary.Contracts;

/// <summary>Единый источник Jwt:SigningKey: fail-fast в не-Development, один dev-фолбэк (BE-066).</summary>
public static class JwtSigningKey
{
    public static byte[] Resolve(string? configured, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Convert.FromBase64String(configured);
        if (!isDevelopment)
            throw new InvalidOperationException("Jwt:SigningKey не задан (fail-fast) — задай env Jwt__SigningKey.");
        return Convert.FromBase64String("eGRpYXJ5LWRldi1zaWduaW5nLWtleS1taW5pbXVtLTMyLWJ5dGVzLTIwMjY=");
    }
}