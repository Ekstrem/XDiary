namespace Partners.Api.Events;
using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using XDiary.Contracts;

/// <summary>Контракт публикации партнёра в Journal (BE-082). Выделен для теста.</summary>
public interface IPartnerEventPublisher
{
    Task PublishAsync(Guid partnerId, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default);
}

/// <summary>
/// BE-082: связь Partners → Journal. Публикует текущее состояние партнёра в
/// `POST /internal/partners/events` (Journal) сервис-токеном `sub=partners`.
/// </summary>
public sealed class PartnerEventPublisher : IPartnerEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _journalUrl;
    private readonly string _signingKey;
    private readonly ILogger<PartnerEventPublisher> _log;
    private static readonly Meter _meter = new("Partners.Events");
    private static readonly Counter<long> _failures = _meter.CreateCounter<long>("journal.publish.failures", description: "не-2xx/исключение при публикации партнёра в Journal");
    public PartnerEventPublisher(IHttpClientFactory http, IConfiguration config, ILogger<PartnerEventPublisher> log)
    { _http = http; _journalUrl = config["Services:Journal"] ?? "http://journal:8080"; _signingKey = config["Jwt:SigningKey"] ?? ""; _log = log; }

    private string CreateServiceToken()
    {
        var key = new SymmetricSecurityKey(JwtSigningKey.Resolve(_signingKey, false));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer: "xdiary", claims: new[] { new Claim("sub", "partners") }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task PublishAsync(Guid partnerId, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_journalUrl}/internal/partners/events");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken());
            req.Content = JsonContent.Create(new { PartnerId = partnerId, Name = name, ClientId = clientId, DeletedAt = deletedAt });
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _failures.Add(1);
                _log.LogWarning("Journal partner publish {PartnerId}: HTTP {Status} (не-2xx)", partnerId, (int)resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _failures.Add(1);
            _log.LogWarning(ex, "Journal partner publish {PartnerId}: канал недоступен (best-effort)", partnerId);
        }
    }
}
