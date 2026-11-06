namespace Client.Application.Security;
using Client.Application.Abstraction;
using Microsoft.Extensions.Logging;
public sealed class LogOtpSender : IOtpSender
{
    private readonly ILogger<LogOtpSender> _log;
    public LogOtpSender(ILogger<LogOtpSender> log) => _log = log;
    public Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default)
    { _log.LogWarning("OTP для {Phone}: транспорт не выбран — код не отправлен (delivery.ok=false).", phone); return Task.FromResult(false); }
}
