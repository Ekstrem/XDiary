namespace Client.Application.Security;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Client.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
public sealed class SmsOtpSender : IOtpSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmsOtpSender> _log;
    public SmsOtpSender(IConfiguration config, ILogger<SmsOtpSender> log) { _config = config; _log = log; }
    public async Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default)
    {
        var url = _config["Otp:Sms:Url"];
        if (string.IsNullOrWhiteSpace(url)) { _log.LogWarning("Otp:Sms:Url не задан — SMS OTP не отправлен (delivery.ok=false)."); return false; }
        var apiKey = _config["Otp:Sms:ApiKey"] ?? string.Empty;
        var sender = _config["Otp:Sms:Sender"] ?? string.Empty;
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(apiKey)) req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent(JsonSerializer.Serialize(new { to = phone, from = sender, message = $"Ваш код: {otp}" }), Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) { _log.LogWarning("SMS OTP не доставлен на {Phone}: HTTP {Code} (delivery.ok=false).", phone, (int)resp.StatusCode); return false; }
        _log.LogInformation("SMS OTP отправлен на {Phone}.", phone);
        return true;
    }
}
