using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>Сессия E2E на клиентском контуре (client-JWT): выдаётся <see cref="E2EInfra.LoginAsync"/>.</summary>
public sealed record E2ESession(string AccessToken, string RefreshToken, Guid ClientId);

/// <summary>
/// Инфраструктура E2E: базовый URL шлюза из XDIARY_E2E_BASE_URL.
/// Если переменная не задана — тесты пропускаются (нужен запущенный стек + Tailscale/локальный compose).
/// </summary>
public static class E2EInfra
{
    public static string BaseUrl => (Environment.GetEnvironmentVariable("XDIARY_E2E_BASE_URL") ?? "").TrimEnd('/');

    /// <summary>Тестовый OTP. На dev канал тестовый: CLIENTS__TESTOTP=000000.</summary>
    public static string Otp => Environment.GetEnvironmentVariable("XDIARY_E2E_OTP") ?? "000000";

    public static bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl);

    public static HttpClient Client()
    {
        var c = new HttpClient { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
        return c;
    }

    private static string NewPhone()
        => "+7999" + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10_000_000).ToString("D7");

    /// <summary>
    /// Логин на КЛИЕНТСКОМ контуре (AR-009 / ADR-021 п.3):
    /// <c>POST /api/v1/clients {phone}</c> → <c>POST /api/v1/clients/{id}/verify {code}</c> →
    /// <c>accessToken</c> + <c>refreshToken</c>.
    ///
    /// Device-token <c>/privacy/token</c> — legacy (серверный PIN вне MVP) и здесь НЕ используется;
    /// именно из-за него 8 из 9 тестов `smoke-api` были помечены `Skip` (TE-041).
    /// </summary>
    public static async Task<E2ESession> LoginAsync(HttpClient c)
    {
        // TE-044/TE-041: коллизия телефона не должна выглядеть как «сервер упал».
        // Пока BE-046 не отдаёт 409/400, дубликат приходит как 409/400/500 с EF-стеком —
        // в этих случаях берём НОВЫЙ телефон и пробуем снова (3 попытки), а если не вышло —
        // бросаем с кодом и телом, а не с «http 500».
        string? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var phone = NewPhone();
            var reg = await c.PostAsJsonAsync("api/v1/clients", new { phone });
            var regText = await reg.Content.ReadAsStringAsync();
            if (!reg.IsSuccessStatusCode)
            {
                var code = (int)reg.StatusCode;
                lastError = $"POST /clients -> {code}: {Truncate(regText)}";
                if (attempt < 3 && IsPhoneCollision(code, regText)) continue;
                throw new InvalidOperationException(lastError);
            }

            var regDoc = JsonSerializer.Deserialize<JsonElement>(regText);
            if (!regDoc.TryGetProperty("id", out var idEl) || !Guid.TryParse(idEl.ToString(), out var clientId))
                throw new InvalidOperationException("POST /clients: в ответе нет id: " + Truncate(regText));

            var ver = await c.PostAsJsonAsync($"api/v1/clients/{clientId}/verify", new { code = Otp });
            var verText = await ver.Content.ReadAsStringAsync();
            if (!ver.IsSuccessStatusCode)
                throw new InvalidOperationException($"POST /clients/{clientId}/verify -> {(int)ver.StatusCode}: {Truncate(verText)}");

            var verDoc = JsonSerializer.Deserialize<JsonElement>(verText);
            var access = verDoc.TryGetProperty("accessToken", out var a) ? a.GetString() : null;
            var refresh = verDoc.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(access))
                throw new InvalidOperationException("verify: accessToken пуст: " + Truncate(verText));

            return new E2ESession(access!, refresh ?? string.Empty, clientId);
        }

        throw new InvalidOperationException("POST /clients: не удалось создать клиента после 3 попыток — " + lastError);
    }

    /// <summary>Признак «телефон уже занят»: 409/400/500 и упоминание дубликата в теле.</summary>
    private static bool IsPhoneCollision(int status, string body)
        => status is 409 or 400 or 500
           && (body.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || body.Contains("23505", StringComparison.Ordinal)
               || body.Contains("already", StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string s) => s.Length <= 300 ? s : s.Substring(0, 300) + "…";


    /// <summary>Обновление пары токенов: <c>POST /api/v1/clients/refresh {refreshToken}</c>.</summary>
    public static async Task<(string AccessToken, string RefreshToken)> RefreshAsync(HttpClient c, string refreshToken)
    {
        var resp = await c.PostAsJsonAsync("api/v1/clients/refresh", new { refreshToken });
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST /clients/refresh -> {(int)resp.StatusCode}: {text}");

        var doc = JsonSerializer.Deserialize<JsonElement>(text);
        var access = doc.TryGetProperty("accessToken", out var a) ? a.GetString() : null;
        var refresh = doc.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
        if (string.IsNullOrWhiteSpace(access))
            throw new InvalidOperationException("refresh: accessToken пуст: " + text);
        return (access!, refresh ?? string.Empty);
    }

    public static HttpClient Authorized(HttpClient c, string token)
    {
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static HttpClient Authorized(HttpClient c, E2ESession session) => Authorized(c, session.AccessToken);

    public static async Task<JsonElement> PostAsync(HttpClient c, string path, object? body = null)
    {
        var resp = await c.PostAsJsonAsync(path, body ?? new { });
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST {path} -> {(int)resp.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    public static async Task<JsonElement> GetAsync(HttpClient c, string path)
    {
        var resp = await c.GetAsync(path);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET {path} -> {(int)resp.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }
}

/// <summary>Fact, который пропускается, если не задан XDIARY_E2E_BASE_URL.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (!E2EInfra.Enabled)
            Skip = "XDIARY_E2E_BASE_URL не задан (нужен запущенный стек: локальный compose или Tailscale).";
    }
}
