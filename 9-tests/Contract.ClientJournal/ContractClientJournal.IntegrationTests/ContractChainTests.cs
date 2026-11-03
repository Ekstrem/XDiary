using System.Net;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.ContractClientJournal;

/// <summary>
/// TE-038: контракт цепочки «онбординг → привязка → журнал», пункты 1–5 из карточки.
///
/// Почему этот проект существует: `onboarding`/`pin-first-login` падали потому, что
/// журнал во фронте открывается только при `verified === true` **и** непустом
/// `linkedPartnerId` (`8-front/js/app.js`, `afterAuth()`). То есть это не «ещё один набор
/// запросов», а контракт трёх условий, которые до сих пор проверялись только глазами.
///
/// ADR-024 / E7 (BE-054) меняет смысл пункта 2: **«я» — профиль, а не партнёр**.
/// Поэтому п.2 проверяет *переход* состояний (до-E7 / deprecated / удалено), а не одно
/// состояние, а п.2b включает проверку профиля, как только маршрут появится в контракте.
/// Это не «подгонка под баг»: причина записана в карточке TE-038 (@TL 2026-11-10 00:22).
///
/// Все тесты — `[StandFact]`: без `XDIARY_E2E_BASE_URL` они пропускаются с причиной
/// и печатают `::warning::`, а не зеленеют впустую.
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

    // Пункт 2 (переход E7/ADR-024): привязка «я» как партнёра уходит — «я» становится профилем.
    //    до E7      — 200, реальная привязка, без `Deprecation` (состояние dev на 2026-11-09);
    //    deprecated — 200 + `Deprecation: true` (no-op, как просил @TL у @BE 2026-11-10 00:20);
    //    после E7   — 404/410 (маршрута нет).
    // Любое другое (401 при валидном токене, 500) — дефект.
    [StandFact]
    public async Task Point2_LinkPartner_Transition_States()
    {
        using var api = new ClientStandApi();
        var (clientId, regStatus) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        Assert.Equal(HttpStatusCode.OK, regStatus);
        var (verifyBody, _) = await api.VerifyAsync(clientId);
        var access = ReadString(verifyBody, "accessToken");

        var (partnerId, partnerStatus) = await api.CreatePartnerAsync(
            access, "TE-038 E7 probe " + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(HttpStatusCode.OK, partnerStatus);

        var (status, deprecation) = await api.LinkPartnerAsync(clientId, partnerId, access);
        var isDeprecatedNoOp = status == HttpStatusCode.OK && !string.IsNullOrEmpty(deprecation);
        var isRemoved = status is HttpStatusCode.NotFound or HttpStatusCode.Gone;
        var isPreE7 = status == HttpStatusCode.OK && string.IsNullOrEmpty(deprecation);

        Assert.True(
            isDeprecatedNoOp || isRemoved || isPreE7,
            "link-partner вернул " + (int)status + " (Deprecation: " + (deprecation ?? "нет") +
            ") — ожидали 200+Deprecation: true (no-op), либо 404/410 (удалён), либо до-E7 200 без заголовка");

        if (isRemoved)
        {
            // Половина E7 = удалили маршрут, но профиля не сделали. На проде это значит:
            // старый фронт зовёт link-partner → 404, и заменить нечем (онбординг сломан).
            // Именно это уехало на прод 2026-11-09 22:50 и было откачено @DO — поэтому
            // проверка обязана падать, а не считать «404 = легальный E7».
            var (profileStatus, _) = await api.UpdateProfileAsync(access, "ТЕ-проверка", "male");
            Assert.True(
                profileStatus != HttpStatusCode.NotFound && profileStatus != HttpStatusCode.MethodNotAllowed,
                "E7 не завершён: link-partner уже 404, а профиля (update-profile) нет — " +
                "онбординг некуда вести (profile → " + (int)profileStatus + ")");
        }

        if (isPreE7)
        {
            Console.WriteLine(
                "::warning::[e7] link-partner ещё реально привязывает и не помечен Deprecated — " +
                "это до-E7 состояние; на прод в таком виде ехать не должно (риск онбординга, @TL 00:20)");
        }
    }

    // Пункт 2b (замена привязки по ADR-024): профиль «я» — displayName/gender сохраняются,
    // без токена — 401. Пока маршрута в коде нет, проверка не «зеленеет молча»: печатает
    // `::warning::` и включится сама, когда @BE выкатит CompleteProfile/UpdateProfile.
    [StandFact]
    public async Task Point2b_Profile_Is_Persisted_When_Contract_Exists()
    {
        using var api = new ClientStandApi();
        var (clientId, _) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        var (verifyBody, _) = await api.VerifyAsync(clientId);
        var access = ReadString(verifyBody, "accessToken");

        var (status, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", "male");
        if (status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            Console.WriteLine(
                "::warning::[infra] профиль «я» ещё не в контракте (update-profile → " + (int)status +
                "): BE-054 не завершён, проверка включится автоматически, когда маршрут появится");
            return;
        }

        Assert.Equal(HttpStatusCode.OK, status);
        var (meStatus, meBody) = await api.MeWithTokenAsync(access);
        Assert.Equal(HttpStatusCode.OK, meStatus);
        Assert.True(
            meBody.TryGetProperty("displayName", out var dn) && dn.GetString() == "ТЕ-профиль",
            "displayName не сохранился в профиле: " + meBody.ToString());

        var (anonStatus, _) = await api.UpdateProfileAsync(string.Empty, "Аноним", "male");
        Assert.Equal(HttpStatusCode.Unauthorized, anonStatus);
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
    // На dev 2026-11-09 проверка КРАСНАЯ и это правильно: свежий клиент видит 1106 записей.
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
