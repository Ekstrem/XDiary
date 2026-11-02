namespace Client.Application.Security;
using System.Net.Http;
using Client.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
public sealed class TelegramOtpSender : IOtpSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramOtpSender> _log;
    public TelegramOtpSender(IConfiguration config, ILogger<TelegramOtpSender> log) { _config = config; _log = log; }
    public async Task SendAsync(string phone, string otp, CancellationToken ct = default)
    {
        var token = _config["Otp:Telegram:BotToken"];
        var chatId = _config["Otp:Telegram:ChatIdMap:" + phone] ?? _config["Otp:Telegram:ChatId"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) { _log.LogWarning("Otp:Telegram:* не задан — Telegram OTP не отправлен."); return; }
        using var http = new HttpClient();
        await http.GetAsync($"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text=" + Uri.EscapeDataString($"Ваш код: {otp}"), ct);
        _log.LogInformation("Telegram OTP отправлен на {Phone}.", phone);
    }
}