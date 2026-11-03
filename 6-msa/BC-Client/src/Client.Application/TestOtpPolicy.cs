namespace Client.Application;
using Microsoft.Extensions.Configuration;
public static class TestOtpPolicy
{
    public static string? Resolve(IConfiguration config, string? environment)
    {
        var isDev = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        var allow = string.Equals(config["Clients:AllowTestOtpOutsideDev"], "true", StringComparison.OrdinalIgnoreCase);
        return (isDev || allow) ? config["Clients:TestOtp"] : null;
    }
}