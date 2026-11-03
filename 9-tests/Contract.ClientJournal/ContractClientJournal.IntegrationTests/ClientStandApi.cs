using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace XDiary.Tests.ContractClientJournal;

/// <summary>
/// TE-038: тонкий клиент контракта вокруг стенда.
///
/// Имена полей и маршрутов вычитаны из кода, а не из названий проектов:
///   POST /api/v1/clients                       — RegisterClientCommand(string Phone)
///   POST /api/v1/clients/{id}/verify           — VerifyClientOtpCommand(Guid, string Code) → accessToken + refreshToken
///   POST /api/v1/clients/refresh               — обмен refresh на новую пару
///   POST /api/v1/clients/{id}/link-partner     — LinkClientPartnerCommand(Guid, Guid PartnerId)
///   GET  /api/v1/clients/me   [Authorize]      — ClientReadModel { id, phone, verified, linkedPartnerId }
///   GET  /api/v1/journal/{recent,places,fantasies} [Authorize]
/// </summary>
public sealed class ClientStandApi : IDisposable
{
    private readonly HttpClient _http;

    public ClientStandApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable(StandFactAttribute.BaseUrlVariable)
                      ?? throw new InvalidOperationException("стенд не задан");
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public static string Otp =>
        Environment.GetEnvironmentVariable("XDIARY_E2E_TEST_OTP") is { Length: > 0 } otp ? otp : "000000";

    public static string UniquePhone() =>
        "+7999" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()[^7..];

    public async Task<(Guid Id, HttpStatusCode Status)> RegisterAsync(string phone)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/clients", new { phone });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var parsed)
            ? parsed
            : Guid.Empty;
        return (id, resp.StatusCode);
    }

    public async Task<(JsonElement Body, HttpStatusCode Status)> VerifyAsync(Guid id, string? code = null)
    {
        using var resp = await _http.PostAsJsonAsync($"api/v1/clients/{id}/verify", new { code = code ?? Otp });
        var body = await ReadJsonAsync(resp);
        return (body, resp.StatusCode);
    }

    public async Task<(JsonElement Body, HttpStatusCode Status)> RefreshAsync(string refreshToken)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/clients/refresh", new { refreshToken });
        var body = await ReadJsonAsync(resp);
        return (body, resp.StatusCode);
    }

    /// <summary>
    /// TE-038: маршрут привязки — ПОД авторизацией (проверено на dev: без токена 401, с токеном 200).
    /// Фронт вызывает его так же — `8-front/js/api.js` `linkClient()` идёт через `post()` с Bearer.
    /// Без токена привязка молча не происходит, а `linkedPartnerId` остаётся null — именно это
    /// держало журнал закрытым в `onboarding`/`pin-first-login`.
    /// </summary>
    public async Task<(HttpStatusCode Status, JsonElement Body)> LinkPartnerAsync(Guid clientId, Guid partnerId, string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"api/v1/clients/{clientId}/link-partner")
        {
            Content = JsonContent.Create(new { partnerId }),
        };
        if (!string.IsNullOrEmpty(accessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var body = await ReadJsonAsync(resp);
        return (resp.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, string? WwwAuthenticate)> MeAsync(string? accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/clients/me");
        if (!string.IsNullOrEmpty(accessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var header = resp.Headers.WwwAuthenticate.ToString();
        return (resp.StatusCode, string.IsNullOrWhiteSpace(header) ? null : header);
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> MeWithTokenAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/clients/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var body = await ReadJsonAsync(resp);
        return (resp.StatusCode, body);
    }

    public async Task<HttpStatusCode> GetAsync(string path, string? accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrEmpty(accessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        return resp.StatusCode;
    }

    public async Task<(Guid Id, HttpStatusCode Status)> CreatePartnerAsync(string accessToken, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/partners")
        {
            Content = JsonContent.Create(new { name }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var body = await ReadJsonAsync(resp);
        var id = body.TryGetProperty("id", out var p) && Guid.TryParse(p.GetString(), out var parsed)
            ? parsed
            : Guid.Empty;
        return (id, resp.StatusCode);
    }

    /// <summary>Регистрация + verify за один шаг: нужен просто валидный client-JWT.</summary>
    public async Task<string> RegisterAndVerifyAsync()
    {
        var (id, status) = await RegisterAsync(UniquePhone());
        if (status != HttpStatusCode.OK)
            throw new InvalidOperationException("POST /clients вернул " + (int)status);
        var (body, verifyStatus) = await VerifyAsync(id);
        if (verifyStatus != HttpStatusCode.OK)
            throw new InvalidOperationException("verify вернул " + (int)verifyStatus);
        return body.GetProperty("accessToken").GetString() ?? string.Empty;
    }

    /// <summary>
    /// BE-053: `POST /api/v1/journal/records` без тела создаёт запись от имени владельца токена
    /// (`StartManualEntry` берёт `sub`). Возвращает id записи.
    /// </summary>
    public async Task<(Guid Id, HttpStatusCode Status)> CreateRecordAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/journal/records");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var body = await ReadJsonAsync(resp);
        var id = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("id", out var p) &&
                 Guid.TryParse(p.GetString(), out var parsed)
            ? parsed
            : Guid.Empty;
        return (id, resp.StatusCode);
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> ListRecordsAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/journal/records");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(req);
        var body = await ReadJsonAsync(resp);
        return (resp.StatusCode, body);
    }

    public List<Guid> ReadIds(JsonElement body)
    {
        var ids = new List<Guid>();
        if (body.ValueKind != JsonValueKind.Array) return ids;
        foreach (var item in body.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var p) &&
                Guid.TryParse(p.GetString(), out var g))
                ids.Add(g);
        }
        return ids;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return default;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Dispose() => _http.Dispose();
}
