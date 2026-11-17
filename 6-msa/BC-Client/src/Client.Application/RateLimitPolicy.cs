namespace Client.Application;
using Microsoft.Extensions.Configuration;

/// <summary>
/// BE-121: в Development лимит OTP отключён (E2E-сценарии переиспользуют один телефон → 5/час
/// ловило 429 и роняло seed). Прод-поведение не меняется: лимит включён, если явно не выключен.
/// </summary>
public static class RateLimitPolicy
{
    public static bool IsDisabled(IConfiguration config, string? environment)
    {
        var isDev = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        var disable = string.Equals(config["Clients:DisableOtpRateLimit"], "true", StringComparison.OrdinalIgnoreCase);
        return isDev || disable;
    }
}
