namespace Client.Application.Security;
using Client.Application.Abstraction;
using Microsoft.Extensions.Logging;
public sealed class LogOtpSender : IOtpSender
{
    private readonly ILogger<LogOtpSender> _log;
    public LogOtpSender(ILogger<LogOtpSender> log) => _log = log;
    public Task SendAsync(string phone, string otp, CancellationToken ct = default)
    { _log.LogInformation("OTP для {Phone}: транспорт не выбран — код не отправлен (код в лог не пишем).", phone); return Task.CompletedTask; }
}