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
    private static string? FindChatId(string map, string phone)
    {
        foreach (var entry in map.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = entry.IndexOf(':');
            if (i < 0) continue;
            if (entry[..i].Trim() == phone) return entry[(i + 1)..].Trim();
        }
        return null;
    }
    public async Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default)
    {
        var token = _config["Otp:Telegram:BotToken"];
        var chatId = FindChatId(_config["Otp:Telegram:ChatIdMap"] ?? string.Empty, phone);
        if (string.IsNullOrWhiteSpace(chatId)) chatId = _config["Otp:Telegram:AdminChatId"] ?? _config["Otp:Telegram:ChatId"];
        if (string.IsNullOrWhiteSpace(token)) { _log.LogWarning("Otp:Telegram:BotToken не задан — OTP не отправлен (delivery.ok=false)."); return false; }
        if (string.IsNullOrWhiteSpace(chatId)) { _log.LogWarning("Номер {Phone} не в Otp:Telegram:ChatIdMap и нет AdminChatId — OTP не отправлен (delivery.ok=false).", phone); return false; }
        var text = $"Код для {phone}: {otp}";
        using var http = new HttpClient();
        var resp = await http.GetAsync($"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text=" + Uri.EscapeDataString(text), ct);
        if (!resp.IsSuccessStatusCode) { _log.LogWarning("Telegram OTP не доставлен на {Phone}: HTTP {Code} (delivery.ok=false).", phone, (int)resp.StatusCode); return false; }
        _log.LogInformation("Telegram OTP отправлен на {Phone}.", phone);
        return true;
    }
}
