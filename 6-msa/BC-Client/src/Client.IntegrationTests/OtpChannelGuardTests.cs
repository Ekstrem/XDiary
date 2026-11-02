using Client.Api.Security;
using Xunit;

public class OtpChannelGuardTests
{
    [Fact]
    public void Log_OutsideDev_Throws() => Assert.Throws<InvalidOperationException>(() => OtpChannelGuard.EnsureValid("log", "Production"));
    [Fact]
    public void Log_InDev_DoesNotThrow() => OtpChannelGuard.EnsureValid("log", "Development");
    [Fact]
    public void Sms_OutsideDev_DoesNotThrow() => OtpChannelGuard.EnsureValid("sms", "Production");
    [Fact]
    public void Telegram_OutsideDev_DoesNotThrow() => OtpChannelGuard.EnsureValid("telegram", "Production");
}