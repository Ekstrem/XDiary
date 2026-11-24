using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

public sealed record E2ESession(string AccessToken, string RefreshToken, Guid ClientId);

public static class E2EInfra
{
    public static string BaseUrl => (Environment.GetEnvironmentVariable("XDIARY_E2E_BASE_URL") ?? "").TrimEnd('/');
    public static bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl);
    private static string Otp => Environment.GetEnvironmentVariable("XDIARY_E2E_OTP") ?? "000000";
    public static HttpClient Client() => new() { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<E2ESession> LoginAsync(HttpClient client)
    {
        string? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var phone = "+7999" + Random.Shared.Next(1_000_000, 9_999_999).ToString();
            var reg = await client.PostAsJsonAsync("api/v1/clients", new { phone });
            var regText = await reg.Content.ReadAsStringAsync();
            if (!reg.IsSuccessStatusCode)
            {
                lastError = $"POST /clients -> {(int)reg.StatusCode}";
                if (attempt < 3 && (reg.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.InternalServerError)) continue;
                throw new InvalidOperationException(lastError);
            }
            var regDoc = JsonSerializer.Deserialize<JsonElement>(regText);
            if (!regDoc.TryGetProperty("id", out var idElement) || !Guid.TryParse(idElement.ToString(), out var clientId))
                throw new InvalidOperationException("POST /clients: в ответе нет id");
            var verify = await client.PostAsJsonAsync($"api/v1/clients/{clientId}/verify", new { code = Otp });
            var verifyText = await verify.Content.ReadAsStringAsync();
            if (!verify.IsSuccessStatusCode)
                throw new InvalidOperationException($"POST /clients/{clientId}/verify -> {(int)verify.StatusCode}");
            var verifyDoc = JsonSerializer.Deserialize<JsonElement>(verifyText);
            var access = verifyDoc.TryGetProperty("accessToken", out var a) ? a.GetString() : null;
            var refresh = verifyDoc.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh))
                throw new InvalidOperationException("verify: не выдан accessToken/refreshToken");
            return new E2ESession(access, refresh, clientId);
        }
        throw new InvalidOperationException(lastError ?? "login failed");
    }
}

public sealed class ApiFactAttribute : FactAttribute
{
    public ApiFactAttribute()
    {
        if (!E2EInfra.Enabled)
        {
            Skip = "XDIARY_E2E_BASE_URL не задан (нужен запущенный стек).";
            Console.WriteLine("::warning::[infra] E2E-тесты пропущены: стенд не задан (XDIARY_E2E_BASE_URL)");
        }
    }
}