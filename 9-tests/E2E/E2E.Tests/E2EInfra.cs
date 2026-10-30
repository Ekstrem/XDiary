using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>
/// Инфраструктура E2E: базовый URL шлюза из XDIARY_E2E_BASE_URL.
/// Если переменная не задана — тесты пропускаются (нужен запущенный стек + Tailscale/локальный compose).
/// </summary>
public static class E2EInfra
{
    public static string BaseUrl => (Environment.GetEnvironmentVariable("XDIARY_E2E_BASE_URL") ?? "").TrimEnd('/');
    public static string Pin => Environment.GetEnvironmentVariable("XDIARY_E2E_PIN") ?? "1234";
    public static bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl);

    public static HttpClient Client()
    {
        var c = new HttpClient { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
        return c;
    }

    public static async Task<string> LoginAsync(HttpClient c)
    {
        var resp = await c.PostAsJsonAsync("api/v1/privacy/token", new { pin = Pin });
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = doc.GetProperty("accessToken").GetString();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("accessToken пуст");
        return token!;
    }

    public static HttpClient Authorized(HttpClient c, string token)
    {
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

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
