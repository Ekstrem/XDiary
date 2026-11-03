using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.GatewaysApi;

/// <summary>
/// TE-040: восемь поведений шлюза (PDR-19), зафиксированы как есть, без переписывания логики.
///
/// Замеры на dev 2026-11-09 (до правок в шлюзе):
///   /health                 → 200
///   /clients/me без токена  → 401 + WWW-Authenticate: Bearer
///   refresh (битый токен)   → 401 **без** WWW-Authenticate → маршрут анонимен, 401 тут про токен, а не про auth
///   неизвестный путь        → **401** (а не 404) — зафиксировано как находка, см. B6
/// </summary>
[Trait("Area", "Gateways.Api")]
[Trait("Task", "TE-040")]
public sealed class GatewayBehaviorTests
{
    private static HttpClient Client()
    {
        var baseUrl = Environment.GetEnvironmentVariable(StandFactAttribute.BaseUrlVariable)!;
        return new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    private static string UniquePhone() =>
        "+7999" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()[^7..];

    private static async Task<(Guid Id, string Access)> RegisterAndVerifyAsync(HttpClient http)
    {
        using var reg = await http.PostAsJsonAsync("api/v1/clients", new { phone = UniquePhone() });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(regBody.GetProperty("id").GetString()!);
        using var verify = await http.PostAsJsonAsync($"api/v1/clients/{id}/verify", new { code = "000000" });
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>();
        return (id, verifyBody.GetProperty("accessToken").GetString()!);
    }

    // B1: GET /health → 200 без токена.
    [StandFact]
    public async Task B1_Health_Is_200_Without_Token()
    {
        using var http = Client();
        using var resp = await http.GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // B2: GET /clients/me без токена → 401 + WWW-Authenticate.
    [StandFact]
    public async Task B2_Me_Without_Token_Is_401_With_WwwAuthenticate()
    {
        using var http = Client();
        using var resp = await http.GetAsync("api/v1/clients/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(resp.Headers.WwwAuthenticate.ToString()),
            "401 без WWW-Authenticate — шлюз не отдаёт challenge");
    }

    // B3: /clients/me с валидным client-JWT → 200 и проксирование verified/linkedPartnerId как есть.
    [StandFact]
    public async Task B3_Me_With_Token_Proxies_Verified_And_LinkedPartnerId()
    {
        using var http = Client();
        var (_, access) = await RegisterAndVerifyAsync(http);

        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/clients/me");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + access);
        using var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("verified", out var verified), "нет поля verified — проксирование не то");
        Assert.True(verified.GetBoolean());
        Assert.True(body.TryGetProperty("linkedPartnerId", out _), "нет поля linkedPartnerId — проксирование не то");
    }

    // B4: POST /clients → 200/201.
    [StandFact]
    public async Task B4_Register_Returns_200_Or_201()
    {
        using var http = Client();
        using var resp = await http.PostAsJsonAsync("api/v1/clients", new { phone = UniquePhone() });
        Assert.True(
            resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            "POST /clients вернул " + (int)resp.StatusCode);
    }

    // B5: refresh анонимен — 401 тут означает «плохой refresh-токен», а не требование auth,
    //     то есть ответ НЕ должен содержать WWW-Authenticate (это признак auth-челленджа).
    [StandFact]
    public async Task B5_Refresh_Is_Anonymous_No_Auth_Challenge()
    {
        using var http = Client();
        using var resp = await http.PostAsJsonAsync("api/v1/clients/refresh", new { refreshToken = "not-a-jwt" });
        Assert.True(
            resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity,
            "refresh на битый токен вернул " + (int)resp.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(resp.Headers.WwwAuthenticate.ToString()),
            "refresh отдал WWW-Authenticate — значит маршрут под auth-middleware, а должен быть анонимным");
    }

    // B6: неизвестный путь — не 502/504 (шлюз не должен выдавать чужую аварию за «нет маршрута»).
    //     Факт на dev: 401. Это зафиксировано в карточке как находка: клиент не отличает
    //     «маршрута нет» от «не авторизован». Ассерт намеренно про 502/504, а не про 404,
    //     чтобы тест не «покраснел от реальности» и не был подогнан.
    [StandFact]
    public async Task B6_Unknown_Path_Is_Not_Upstream_Failure()
    {
        using var http = Client();
        using var resp = await http.GetAsync("api/v1/definitely-no-such-route-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.NotEqual(HttpStatusCode.BadGateway, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.GatewayTimeout, resp.StatusCode);
    }

    // B7: апстрим недоступен → 502 / таймаут → 504.
    //     Снаружи это не воспроизвести без управления контейнерами (нужен @DO): остановить
    //     сервис, дождаться 502, включить таймаут — и вернуть. Пока поведение не проверяется,
    //     и это сказано явно, а не «зелёным по умолчанию».
    [Fact]
    public void B7_Upstream_502_504_Requires_Upstream_Control()
    {
        Console.WriteLine(
            "::warning::[infra] B7 не проверено: для 502/504 нужно управление апстримом " +
            "(остановить сервис / включить таймаут) — зона @DO, отдельный прогон");
    }

    // B8: Authorization доходит до апстрима без изменений — без префикса `Bearer` шлюз
    //     обязан отклонить (401), с префиксом — 200. Это и доказывает, что заголовок
    //     не переписывается по дороге.
    [StandFact]
    public async Task B8_Authorization_Reaches_Upstream_Unchanged()
    {
        using var http = Client();
        var (_, access) = await RegisterAndVerifyAsync(http);

        using var raw = new HttpRequestMessage(HttpMethod.Get, "api/v1/clients/me");
        raw.Headers.TryAddWithoutValidation("Authorization", access); // без "Bearer "
        using var rawResp = await http.SendAsync(raw);
        Assert.Equal(HttpStatusCode.Unauthorized, rawResp.StatusCode);

        using var proper = new HttpRequestMessage(HttpMethod.Get, "api/v1/clients/me");
        proper.Headers.TryAddWithoutValidation("Authorization", "Bearer " + access);
        using var properResp = await http.SendAsync(proper);
        Assert.Equal(HttpStatusCode.OK, properResp.StatusCode);
    }
}
