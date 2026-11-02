namespace Client.Api.Security;
public static class OtpChannelGuard
{
    public static void EnsureValid(string? channel, string? environment)
    {
        var isDev = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        if (channel == "log" && !isDev)
            throw new InvalidOperationException("Otp:Channel=log допустим только в dev/CI; на проде задай sms|telegram.");
    }
}