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

        // Пустые значения обязана отклонять валидация домена, а не сохранять.
        // Расхождение по словарю `gender` зафиксировано отдельно ниже (см. gender-канон).
        var (emptyName, _) = await api.UpdateProfileAsync(access, string.Empty, "male");
        Assert.True(
            emptyName is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            "пустой displayName должен отклоняться 400, а вернул " + (int)emptyName);

        var (emptyGender, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", string.Empty);
        Assert.True(
            emptyGender is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            "пустой gender должен отклоняться 400, а вернул " + (int)emptyGender);

        // gender-канон (@AR 2026-11-10 03:12): словарь `male`/`female`, неизвестное значение → 400.
        // Идея @AR: «тест на неизвестное значение ценнее, чем на известное — он ловит момент,
        // когда словарь снова станет свободной строкой». Поэтому проверяем не одно значение,
        // а три варианта написания, которыми дрейф и проявлялся (`Other` / `other` / `м`).
        foreach (var bad in new[] { "Other", "other", "м" })
        {
            var (badGender, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", bad);
            Assert.True(
                badGender is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
                "gender='" + bad + "' должен отклоняться 400 по словарю male/female (@AR 03:12), а вернул " +
                (int)badGender + " — контракт всё ещё принимает свободную строку (@BE 04:12)");
        }

        // Известное значение по-прежнему принимается (иначе «валидация» сломала бы онбординг).
        var (femaleOk, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", "female");
        Assert.Equal(HttpStatusCode.OK, femaleOk);

        // Нормализация — часть согласованной формы фикса (@AR 2026-11-10 05:43:
        // `Trim().ToLowerInvariant()` + словарь в агрегате). Поэтому `"Male"` и `" female "`
        // обязаны приниматься (это те же значения после нормализации), а не отклоняться.
        // Если @BE сделает словарь без нормализации — тест скажет об этом, а не «промолчит».
        var (caseOk, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", "Male");
        Assert.Equal(HttpStatusCode.OK, caseOk);

        var (spacedOk, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", " female ");
        Assert.Equal(HttpStatusCode.OK, spacedOk);

        // Канонизация при сохранении (@AR 2026-11-10 10:13, ADR-024 §3): в модель попадает
        // НОРМАЛИЗОВАННОЕ значение, а не исходная строка. Иначе в базе появятся `Male`, `male `,
        // и та же грязь вернётся в аналитику — при том, что тест «принимается» был бы зелёным.
        var (_, canonBody) = await api.MeWithTokenAsync(access);
        Assert.True(
            canonBody.TryGetProperty("gender", out var canonGender) && canonGender.GetString() == "female",
            "после ' female ' в профиле должно лежать каноническое 'female', а лежит " +
            (canonBody.TryGetProperty("gender", out var g0) ? "'" + g0.GetString() + "'" : "поле отсутствует"));

        var (maleOk, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", "Male");
        Assert.Equal(HttpStatusCode.OK, maleOk);
        var (_, maleBody) = await api.MeWithTokenAsync(access);
        Assert.True(
            maleBody.TryGetProperty("gender", out var maleGender) && maleGender.GetString() == "male",
            "после 'Male' в профиле должно лежать каноническое 'male', а лежит " +
            (maleBody.TryGetProperty("gender", out var g1) ? "'" + g1.GetString() + "'" : "поле отсутствует"));

        // @AR 2026-11-10 12:52: нормализация = `Trim` + нижний регистр, значит `"  male  "`
        // (двойные пробелы) → 200 и в ответе ровно `male`. Если вернётся `"  male  "` как есть —
        // это дефект реализации (не нормализовано перед сохранением), и ловим его отдельной строкой,
        // а не только по статусу.
        var (multiSpaceOk, _) = await api.UpdateProfileAsync(access, "ТЕ-профиль", "  male  ");
        Assert.Equal(HttpStatusCode.OK, multiSpaceOk);
        var (_, multiSpaceBody) = await api.MeWithTokenAsync(access);
        Assert.True(
            multiSpaceBody.TryGetProperty("gender", out var msGender) && msGender.GetString() == "male",
            "после '  male  ' в профиле должно лежать каноническое 'male', а лежит " +
            (multiSpaceBody.TryGetProperty("gender", out var g2) ? "'" + g2.GetString() + "'" : "поле отсутствует"));

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

    // Пункт 6 (@TL 2026-11-10 04:45, новая находка): `GET /api/v1/clients` отдавал список ВСЕХ
    // клиентов с телефонами и был анонимным; `GET /clients/{id}` тоже. Это дыра того же класса,
    // что утренняя с партнёрами, и закрывать её надо до любого показа продукта.
    //
    // Целевой контракт: без токена — 401 (а не 200 со списком); с токеном A чужой id → 403/404.
    // На dev 2026-11-10 проверка ожидаемо КРАСНАЯ, пока @BE не задеплоил фикс — и это её работа.
    [StandFact]
    public async Task Point6_ClientList_And_ForeignId_Require_Ownership()
    {
        using var api = new ClientStandApi();

        var (anonStatus, anonBody) = await api.ListClientsAsync(null);
        Assert.True(
            anonStatus == HttpStatusCode.Unauthorized || anonStatus == HttpStatusCode.Forbidden,
            "GET /api/v1/clients без токена вернул " + (int)anonStatus +
            " — список клиентов с телефонами не должен быть анонимным (@TL 04:45)");

        var tokenA = await api.RegisterAndVerifyAsync();
        var tokenB = await api.RegisterAndVerifyAsync();

        // Чужой id: узнаём его через свой список (если список вообще что-то отдаёт) либо через
        // создание B — нам достаточно факта, что A не может прочитать B.
        var (listStatus, listBody) = await api.ListClientsAsync(tokenA);
        if (listStatus == HttpStatusCode.OK)
        {
            var ids = api.ReadIds(listBody);
            Assert.True(
                ids.Count <= 1,
                "GET /api/v1/clients с токеном вернул " + ids.Count +
                " клиентов — должно быть только «я» (иначе утечка телефонов)");
        }

        // Прямая попытка: A по чужому id (id клиента B нам неизвестен из контракта, поэтому
        // проверяем на случайном валидном GUID — маршрут не должен отдавать 200 с чужими данными).
        var foreign = await api.GetAsync("api/v1/clients/" + Guid.NewGuid(), tokenA);
        Assert.True(
            foreign is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
            "GET /clients/{чужой id} вернул " + (int)foreign + " — ожидали 403/404/401 (@TL 04:45)");

        // ADR-026 (после @AR 2026-11-10 14:52) фиксирует по каждому маршруту отдельно:
        //   `GET /api/v1/clients` (список) — УДАЛИТЬ → анонимно 401/404;
        //   `GET /api/v1/clients/{id}`     — оставить, [Authorize] + только «себя», иначе **403**.
        // Поэтому 404 на чужой id — не «допустимый диапазон», а признак того, что проверка
        // владельца, вероятно, отсутствует: забыли `Forbid` — получили 404 и «всё в порядке».
        if (foreign == HttpStatusCode.NotFound)
        {
            Console.WriteLine(
                "::warning::[spec] маршрут GET /api/v1/clients/{id} (чужой id) → 404; " +
                "требование ADR-026 §3 / BE-064: [Authorize] + только «себя», иначе **403**. " +
                "404 на существующий чужой ресурс = проверка владельца, вероятно, отсутствует " +
                "(забыли Forbid — и расхождение не видно)");
        }

        // И без токена тот же маршрут обязан требовать авторизацию.
        var anonForeign = await api.GetAsync("api/v1/clients/" + Guid.NewGuid(), null);
        Assert.True(
            anonForeign is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            "GET /clients/{id} без токена вернул " + (int)anonForeign + " — ожидали 401");
    }

    // Пункт 7 — матрица ADR-026 §3 (@AR 2026-11-10 08:33 + 09:02, @TL 08:31 + 09:01).
    // Ключ анонимности — ПАРА (метод, путь); всё остальное deny-by-default.
    //
    // Порядок строк намеренно от сильного к слабому: сначала «посторонний» маршрут анонимно
    // (без него deny-by-default нечем доказать), потом allow-list по смыслу, потом внутренний контур.
    [StandFact]
    public async Task Point7_Adr026_Anonymous_Matrix()
    {
        using var api = new ClientStandApi();

        // (1) «Посторонний» маршрут анонимно → 401/404. `GET /clients` по ADR-026 удаляется,
        //     поэтому 404 — валидный исход; важно, что НЕ 200.
        var strangers = new[]
        {
            "api/v1/clients",
            "api/v1/journal/records",
            "api/v1/partners",
        };
        foreach (var path in strangers)
        {
            var status = await api.GetAsync(path, null);
            Assert.True(
                status is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                "GET " + path + " без токена вернул " + (int)status +
                " — deny-by-default не работает (ADR-026 §3)");
        }

        // (2) Allow-list анонимно — по смыслу, онбординг обязан работать без токена.
        var (_, regStatus) = await api.RegisterAsync(ClientStandApi.UniquePhone());
        Assert.True(
            regStatus is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.BadRequest,
            "POST /clients без токена вернул " + (int)regStatus +
            " — онбординг не должен требовать токен (ADR-026 allow-list)");

        var (_, verifyStatus) = await api.VerifyAsync(Guid.NewGuid(), "000000");
        Assert.True(
            verifyStatus is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            "POST /clients/{id}/verify без токена вернул " + (int)verifyStatus +
            " — ожидали 400/401/404 по смыслу (не 405 и не 500)");

        var (refreshBody, refreshStatus) = await api.RefreshAsync("not-a-jwt");
        _ = refreshBody;
        Assert.True(
            refreshStatus is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity,
            "POST /clients/refresh без токена вернул " + (int)refreshStatus +
            " — ожидали 400/401 (не 405: маршрут должен существовать)");

        Assert.Equal(HttpStatusCode.OK, await api.GetAsync("health", null));

        // (3) Внутренний контур: `GET /internal/journal/records` анонимно → 200.
        //     Им Analytics обновляет «Тренды». Если @BE включит default-deny без исключения
        //     для `/internal/**`, «Тренды» замрут молча — это и ловит строка.
        //     Через шлюз маршрут может быть не опубликован (шлюз публикует только `/api/v1/**`),
        //     и тогда проверка честно говорит, что её надо делать внутри сети (зона @DO).
        var internalStatus = await api.GetAsync("internal/journal/records", null);
        if (internalStatus == HttpStatusCode.NotFound)
        {
            Console.WriteLine(
                "::warning::[infra] GET /internal/journal/records через шлюз = 404 (маршрут не публикуется) — " +
                "проверка внутреннего контура возможна только изнутри сети, зона @DO");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, internalStatus);
        }
    }

    // Пункт 8 (@AR 2026-11-10 18:03, TECHDEBT п.16): канон пола живёт в ДВУХ местах —
    // inline в `Client.Aggregate.UpdateProfile` (покрыт `Point2b`) и через `GenderEnumValidator`
    // в `Partners.Aggregate.UpdateProfile`. Второй путь контрактом не покрывался, а именно так
    // «два источника правды» и расходятся незаметно: клиент уже валидирует, партнёр — ещё нет.
    //
    // Ожидание: `"Male"` → 200 и в ответе **`male`** (канон, а не исходная строка); `Other` → 400.
    [StandFact]
    public async Task Point8_PartnersProfile_GenderCanon()
    {
        using var api = new ClientStandApi();
        var token = await api.RegisterAndVerifyAsync();

        var (partnerId, partnerStatus) = await api.CreatePartnerAsync(
            token, "TE-038 партнёр " + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(HttpStatusCode.OK, partnerStatus);
        Assert.NotEqual(Guid.Empty, partnerId);

        var (canonStatus, canonBody) = await api.UpdatePartnerProfileAsync(partnerId, token, "Male");
        Assert.Equal(HttpStatusCode.OK, canonStatus);
        Assert.True(
            canonBody.TryGetProperty("gender", out var g) && g.GetString() == "male",
            "после 'Male' в профиле партнёра должен лежать канон 'male', а лежит " +
            (canonBody.TryGetProperty("gender", out var g0) ? "'" + g0.GetString() + "'" : "поле отсутствует"));

        var (otherStatus, _) = await api.UpdatePartnerProfileAsync(partnerId, token, "Other");
        Assert.True(
            otherStatus is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            "gender='Other' в профиле партнёра должен отклоняться 400, а вернул " + (int)otherStatus +
            " — канон @AR реализован только в Client, второй путь отстал (TECHDEBT п.16)");
    }
}
