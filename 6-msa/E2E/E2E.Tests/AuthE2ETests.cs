using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>
/// TE-041: E2E аутентификация на КЛИЕНТСКОМ контуре (AR-009 / ADR-021 п.3).
/// Раньше набор ходил в legacy `POST /privacy/token {pin}` и был выключен через `Skip`;
/// теперь проверяется рабочий контур: `/clients` → `/verify` → client-JWT → `/refresh`.
/// </summary>
public class AuthE2ETests
{
    [E2EFact]
    public async Task Login_ReturnsAccessAndRefresh()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken), "accessToken выдан");
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken), "refreshToken выдан");
        Assert.NotEqual(Guid.Empty, session.ClientId);
    }

    [E2EFact]
    public async Task Anonymous_IsRejected_401_WithWwwAuthenticate()
    {
        var c = E2EInfra.Client();
        var resp = await c.GetAsync("api/v1/clients/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.True(
            resp.Headers.WwwAuthenticate.Count > 0,
            "401 должен содержать заголовок WWW-Authenticate (BE-044)");
    }

    [E2EFact]
    public async Task ValidToken_AllowsAccess()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        var me = await E2EInfra.GetAsync(c, "api/v1/clients/me");
        Assert.True(me.TryGetProperty("id", out _) || me.TryGetProperty("Id", out _), "GET /clients/me вернул клиента");
    }

    [E2EFact]
    public async Task Refresh_ReturnsNewPair()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);

        var (access, refresh) = await E2EInfra.RefreshAsync(c, session.RefreshToken);

        Assert.False(string.IsNullOrWhiteSpace(access), "refresh выдал новый accessToken");
        Assert.False(string.IsNullOrWhiteSpace(refresh), "refresh выдал новый refreshToken");
    }

    [E2EFact]
    public async Task Logout_ClearsTokens_ThenAnonymous401()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        // logout на клиенте = очистка токенов (JWT stateless)
        c.DefaultRequestHeaders.Authorization = null;

        var resp = await c.GetAsync("api/v1/clients/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [E2EFact]
    public async Task Journal_Anonymous_IsRejected_401()
    {
        var c = E2EInfra.Client();
        var resp = await c.GetAsync("api/v1/journal/records");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [E2EFact]
    public async Task Journal_WithClientToken_AllowsAccess()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        var resp = await c.GetAsync("api/v1/journal/records");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // Тест «/privacy/token больше не работает» удалён осознанно: критерий TE-041 —
    // «ни один тест гейта не ходит в /privacy/token и /privacy/pin/*». Контур закрыт
    // решением AR-009 (legacy) и на фронте проверяется через `no-pin.spec.js` (skip).
}
