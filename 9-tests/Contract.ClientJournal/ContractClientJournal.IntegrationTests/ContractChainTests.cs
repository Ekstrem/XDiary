using System.Net;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.ContractClientJournal;

/// <summary>
/// TE-038: контракт цепочки «онбординг → привязка → журнал», пункты 1–5 из карточки.
///
/// Почему этот проект вообще существует: `onboarding`/`pin-first-login` падали потому, что
/// журнал во фронте открывается только при `verified === true` **и** непустом
/// `linkedPartnerId` (`8-front/js/app.js`, `afterAuth()`). То есть это не «ещё один набор
/// запросов», а контракт трёх условий, которые до сих пор проверялись только глазами.
///
/// Все тесты — `[StandFact]`: без `XDIARY_E2E_BASE_URL` они пропускаются с причиной,
/// а не зеленеют впустую.
/// </summary>
[Trait("Area", "Contract.ClientJournal")]
[Trait("Task", "TE-038")]
public sealed class ContractChainTests
{
    private static string ReadString(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? string.Empty
            : string.Empty;

    private static Guid ReadGuid(JsonElement body, string name) =>
        Guid.TryParse(ReadString(body, name), out var g) ? g : Guid.Empty;

    // Пункт 1: POST /clients → /verify → GET /clients/me отдаёт verified === true.
    [StandFact]
    public async Task Point1_Onboarding_Me_Returns_Verified_True()
    {
        using var api = new ClientStandApi();
        var (id, regStatus) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        Assert.Equal(HttpStatusCode.OK, regStatus);
        Assert.NotEqual(Guid.Empty, id);

        var (verifyBody, verifyStatus) = await api.VerifyAsync(id);
        Assert.Equal(HttpStatusCode.OK, verifyStatus);
        var access = ReadString(verifyBody, "accessToken");
        Assert.False(string.IsNullOrWhiteSpace(access), "verify должен вернуть accessToken");

        var (meStatus, meBody) = await api.MeWithTokenAsync(access);
        Assert.Equal(HttpStatusCode.OK, meStatus);
        Assert.True(
            meBody.GetProperty("verified").GetBoolean(),
            "после verify GET /clients/me обязан отдавать verified=true — иначе фронт не откроет журнал");
    }

    // Пункт 2: link-partner → GET /clients/me отдаёт непустой linkedPartnerId.
    [StandFact]
    public async Task Point2_LinkPartner_Me_Returns_LinkedPartnerId()
    {
        using var api = new ClientStandApi();
        var (clientId, _) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        var (verifyBody, _) = await api.VerifyAsync(clientId);
        var access = ReadString(verifyBody, "accessToken");

        var (partnerId, partnerStatus) = await api.CreatePartnerAsync(access, "TE-038 partner " + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(HttpStatusCode.OK, partnerStatus);
        Assert.NotEqual(Guid.Empty, partnerId);

        var (linkStatus, _) = await api.LinkPartnerAsync(clientId, partnerId, access);
        Assert.Equal(HttpStatusCode.OK, linkStatus);

        // Обратный контроль к пункту 2: без токена привязка обязана быть отклонена (401),
        // иначе «связалось» могло бы означать что угодно. Проверено на dev 2026-11-09.
        var (anonLinkStatus, _) = await api.LinkPartnerAsync(clientId, partnerId, string.Empty);
        Assert.Equal(HttpStatusCode.Unauthorized, anonLinkStatus);

        var (meStatus, meBody) = await api.MeWithTokenAsync(access);
        Assert.Equal(HttpStatusCode.OK, meStatus);
        Assert.Equal(partnerId, ReadGuid(meBody, "linkedPartnerId"));
    }

    // Пункт 3: журнал под токеном — 200, без токена — 401.
    [StandFact]
    public async Task Point3_Journal_Requires_ClientJwt()
    {
        using var api = new ClientStandApi();
        var (clientId, _) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        var (verifyBody, _) = await api.VerifyAsync(clientId);
        var access = ReadString(verifyBody, "accessToken");

        foreach (var path in new[] { "api/v1/journal/recent", "api/v1/journal/places", "api/v1/journal/fantasies" })
        {
            Assert.Equal(HttpStatusCode.OK, await api.GetAsync(path, access));
            Assert.Equal(HttpStatusCode.Unauthorized, await api.GetAsync(path, null));
        }
    }

    // Пункт 4: /verify отдаёт refreshToken; refresh выдаёт новую пару; старый access ещё работает.
    [StandFact]
    public async Task Point4_Refresh_Returns_NewPair_And_OldAccessStillWorks()
    {
        using var api = new ClientStandApi();
        var (clientId, _) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        var (verifyBody, _) = await api.VerifyAsync(clientId);
        var access = ReadString(verifyBody, "accessToken");
        var refresh = ReadString(verifyBody, "refreshToken");
        Assert.False(string.IsNullOrWhiteSpace(refresh), "verify обязан возвращать refreshToken (BE-041)");

        var (refreshBody, refreshStatus) = await api.RefreshAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, refreshStatus);
        var newAccess = ReadString(refreshBody, "accessToken");
        var newRefresh = ReadString(refreshBody, "refreshToken");
        Assert.False(string.IsNullOrWhiteSpace(newAccess), "refresh обязан вернуть новый accessToken");
        Assert.False(string.IsNullOrWhiteSpace(newRefresh), "refresh обязан вернуть новый refreshToken");
        Assert.NotEqual(access, newAccess);

        // Контракт: access живёт до истечения срока, refresh его не инвалидирует.
        var (oldAccessStatus, _) = await api.MeWithTokenAsync(access);
        Assert.Equal(HttpStatusCode.OK, oldAccessStatus);
    }

    // Пункт 5: /clients/me без токена — 401 именно с WWW-Authenticate.
    [StandFact]
    public async Task Point5_Me_Without_Token_Is_401_With_WwwAuthenticate()
    {
        using var api = new ClientStandApi();
        var (status, header) = await api.MeAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.False(string.IsNullOrWhiteSpace(header), "401 без WWW-Authenticate — контракт не тот (BE-044)");
        Assert.Contains("Bearer", header, StringComparison.OrdinalIgnoreCase);
    }

    // Требование @TL 20:40 п.3 (фиксация BE-053): журнал скоупится по клиенту.
    // Целевой контракт: без токена 401; с токеном A — только записи A; чужая запись → 403/404, не 200.
    //
    // На dev 2026-11-09 проверка КРАСНАЯ и это правильно: свежий клиент получает 1100 записей,
    // а запись клиента B читается токеном A с ответом 200. То есть BE-053 ещё не в образе —
    // тест фиксирует требуемое поведение, а не текущее.
    [StandFact]
    public async Task Point3b_JournalScope_OnlyOwn_And_Foreign_Is_403_Or_404()
    {
        using var api = new ClientStandApi();

        var tokenA = await api.RegisterAndVerifyAsync();
        var tokenB = await api.RegisterAndVerifyAsync();

        var (recordA, statusA) = await api.CreateRecordAsync(tokenA);
        var (recordB, statusB) = await api.CreateRecordAsync(tokenB);
        Assert.Equal(HttpStatusCode.OK, statusA);
        Assert.Equal(HttpStatusCode.OK, statusB);
        Assert.NotEqual(Guid.Empty, recordA);
        Assert.NotEqual(Guid.Empty, recordB);

        var (listStatus, listBody) = await api.ListRecordsAsync(tokenA);
        Assert.Equal(HttpStatusCode.OK, listStatus);
        var ids = api.ReadIds(listBody);
        Assert.Contains(recordA, ids);
        Assert.DoesNotContain(recordB, ids);

        var own = await api.GetAsync("api/v1/journal/records/" + recordA, tokenA);
        Assert.Equal(HttpStatusCode.OK, own);

        var foreign = await api.GetAsync("api/v1/journal/records/" + recordB, tokenA);
        Assert.True(
            foreign == HttpStatusCode.Forbidden || foreign == HttpStatusCode.NotFound,
            "чужая запись должна отдавать 403/404, а не " + (int)foreign + " — иначе это утечка между клиентами (BE-053)");

        Assert.Equal(HttpStatusCode.Unauthorized, await api.GetAsync("api/v1/journal/records", null));
    }
}
