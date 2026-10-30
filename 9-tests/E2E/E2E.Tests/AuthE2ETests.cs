using System.Net;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>TE-007: E2E аутентификация (логин → JWT → refresh → logout).</summary>
public class AuthE2ETests
{
    [E2EFact]
    public async Task Login_ReturnsAccessAndRefresh()
    {
        var c = E2EInfra.Client();
        var resp = await c.PostAsJsonAsync("api/v1/privacy/token", new { pin = E2EInfra.Pin });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var doc = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(doc.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(doc.GetProperty("refreshToken").GetString()));
    }

    [E2EFact]
    public async Task Anonymous_IsRejected_401()
    {
        var c = E2EInfra.Client();
        var resp = await c.GetAsync("api/v1/journal/records");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [E2EFact]
    public async Task ValidToken_AllowsAccess()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        var resp = await c.GetAsync("api/v1/journal/records");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [E2EFact]
    public async Task Logout_ClearsTokens_ThenAnonymous401()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        // logout на клиенте = очистка токенов (JWT stateless)
        c.DefaultRequestHeaders.Authorization = null;

        var resp = await c.GetAsync("api/v1/journal/records");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
