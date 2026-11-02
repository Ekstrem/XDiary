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
    public async Task SendAsync(string phone, string otp, CancellationToken ct = default)
    {
        var token = _config["Otp:Telegram:BotToken"];
        var chatId = FindChatId(_config["Otp:Telegram:ChatIdMap"] ?? string.Empty, phone);
        if (string.IsNullOrWhiteSpace(chatId)) chatId = _config["Otp:Telegram:AdminChatId"] ?? _config["Otp:Telegram:ChatId"];
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Otp:Telegram:BotToken не задан.");
        if (string.IsNullOrWhiteSpace(chatId)) throw new InvalidOperationException($"Номер {phone} не в Otp:Telegram:ChatIdMap и нет Otp:Telegram:AdminChatId — OTP не отправлен.");
        var text = $"Код для {phone}: {otp}";
        using var http = new HttpClient();
        var resp = await http.GetAsync($"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text=" + Uri.EscapeDataString(text), ct);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"Telegram {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
        _log.LogInformation("Telegram OTP отправлен на {Phone}.", phone);
    }
}