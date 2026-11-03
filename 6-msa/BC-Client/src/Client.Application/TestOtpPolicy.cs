namespace Client.Application;
using Microsoft.Extensions.Configuration;
public static class TestOtpPolicy
{
    public static string? Resolve(IConfiguration config, string? environment, out bool warnOutsideDev)
    {
        var isDev = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        var allow = string.Equals(config["Clients:AllowTestOtpOutsideDev"], "true", StringComparison.OrdinalIgnoreCase);
        var code = config["Clients:TestOtp"];
        warnOutsideDev = allow && !isDev && code is not null;
        return (isDev || allow) ? code : null;
    }
}