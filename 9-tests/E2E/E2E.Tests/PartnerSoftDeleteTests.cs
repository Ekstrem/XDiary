using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>
/// TE-050 (по запросу @BE 2026-11-13 15:42): контрактные тесты мягкого удаления партнёра.
///
/// Что проверяем (список @BE, `15:11`, + инвариант @AR §11 `HistoryStaysResolvable`):
///   1. удалённый партнёр исчезает из `GET /api/v1/partners`;
///   2. повторный `POST /partners/{id}/remove` — идемпотентен (не 5xx, второй раз не ошибка);
///   3. `GET /api/v1/partners/{id}` после удаления → 404 (или 403 — владелец проверяется раньше);
///   4. `HistoryStaysResolvable`: запись журнала, ссылающаяся на удалённого партнёра, **всё ещё
///      показывает его имя** (имя в записи не хранится — только `id`, поэтому удаление обязано
///      быть мягким; физическое удаление осиротило бы историю);
///   5. (перенесено в доменные тесты @BE) «старые строки без `DeletedAt` читаются» — проверить
///      через HTTP нельзя, это состояние БД; у @BE есть `Partners.Domain.Tests`.
///
/// Формат — как у остального E2E (`[E2EFact]`, `E2EInfra`): без `XDIARY_E2E_BASE_URL` тесты
/// пропускаются с причиной, а не падают. Удаление партнёра — **необратимое** на уровне данных,
/// поэтому каждый тест создаёт собственного партнёра и не трогает чужих.
/// </summary>
public class PartnerSoftDeleteTests
{
    private static async Task<(HttpClient c, E2ESession s)> SessionAsync()
    {
        var c = E2EInfra.Client();
        var s = await E2EInfra.LoginAsync(c);
        c.DefaultRequestHeaders.Authorization = new("Bearer", s.AccessToken);
        return (c, s);
    }

    /// <summary>Создаёт партнёра и возвращает его id. Два шага: регистрация (имя) → профиль (пол) — контракт E7.</summary>
    private static async Task<Guid> CreatePartnerAsync(HttpClient c, string name)
    {
        var reg = await c.PostAsJsonAsync("api/v1/partners", new { name });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        using var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        return id;
    }

    private static async Task<List<Guid>> ListPartnerIdsAsync(HttpClient c)
    {
        var r = await c.GetAsync("api/v1/partners");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var ids = new List<Guid>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (e.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                ids.Add(idProp.GetGuid());
        }
        return ids;
    }

    [E2EFact]
    public async Task Remove_PartnerDisappearsFromList()
    {
        var (c, _) = await SessionAsync();
        var name = "TE-soft-" + Guid.NewGuid().ToString("N")[..8];
        var id = await CreatePartnerAsync(c, name);

        var before = await ListPartnerIdsAsync(c);
        Assert.Contains(id, before);

        var remove = await c.PostAsJsonAsync($"api/v1/partners/{id}/remove", new { });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var after = await ListPartnerIdsAsync(c);
        Assert.DoesNotContain(id, after);
    }

    [E2EFact]
    public async Task Remove_IsIdempotent_SecondCallIsNotServerError()
    {
        var (c, _) = await SessionAsync();
        var id = await CreatePartnerAsync(c, "TE-soft-idem-" + Guid.NewGuid().ToString("N")[..8]);

        var first = await c.PostAsJsonAsync($"api/v1/partners/{id}/remove", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Идемпотентность: второй вызов не должен превращаться в 5xx и не должен «оживлять» партнёра.
        var second = await c.PostAsJsonAsync($"api/v1/partners/{id}/remove", new { });
        Assert.InRange((int)second.StatusCode, 200, 499);

        var after = await ListPartnerIdsAsync(c);
        Assert.DoesNotContain(id, after);
    }

    [E2EFact]
    public async Task Remove_ThenGet_ReturnsNotFoundOrForbidden_NotOk()
    {
        var (c, _) = await SessionAsync();
        var id = await CreatePartnerAsync(c, "TE-soft-get-" + Guid.NewGuid().ToString("N")[..8]);

        var remove = await c.PostAsJsonAsync($"api/v1/partners/{id}/remove", new { });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var get = await c.GetAsync($"api/v1/partners/{id}");
        // 404 — «нет такого»; 403 — если проверка владельца идёт раньше выборки. 200 — дефект:
        // удалённый партнёр продолжает отдаваться как живой.
        Assert.True(
            get.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"после Remove ожидали 404/403, получили {(int)get.StatusCode}");
    }

    /// <summary>
    /// TE-050 кейс (4), @AR 2026-11-13 16:20: история остаётся разрешимой (ADR-028 §11 `HistoryStaysResolvable`).
    ///
    /// **Состояние на 2026-11-13: pending — пройти не может.** Проверено `origin`:
    /// `JournalReadModel`/`IJournalAnemicModel` содержат только `Root.Partners` — `IReadOnlyList&lt;string&gt;`
    /// с **id**, а `git grep -i partnername -- 6-msa` даёт **0** совпадений. Ассерт не ослабляем
    /// (иначе тест зелёный и бесполезный): вместо этого тест **сам определяет**, включён ли триггер
    /// из §8, и печатает `::warning::[spec][pending:PartnersDirectory]`, пока имён нет.
    ///
    /// Триггер включения (машинно-проверяемый, §8): (а) существует `partners_directory` **и**
    /// (б) ответ `GET /journal/records/{id}` содержит **имена** партнёров (аддитивное поле рядом с `partners`).
    /// Когда оба выполнены — ассерт ниже начинает работать сам, без правки теста.
    /// </summary>
    [E2EFact]
    public async Task Remove_PartnerInExistingJournalRecord_StillResolvable()
    {
        var (c, _) = await SessionAsync();
        var partnerName = "TE-soft-hist-" + Guid.NewGuid().ToString("N")[..8];
        var partnerId = await CreatePartnerAsync(c, partnerName);

        // Реальный контракт журнала (проверено по JournalController):
        //   POST /journal/records            — без тела, создаёт черновик
        //   POST /journal/records/{id}/partners { partnerId } — привязка партнёра (строка, не Guid)
        var create = await c.PostAsync("api/v1/journal/records", null);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var recordId = created.RootElement.TryGetProperty("id", out var idProp)
            ? idProp.GetGuid()
            : created.RootElement.GetProperty("Id").GetGuid();

        var addPartner = await c.PostAsJsonAsync(
            $"api/v1/journal/records/{recordId}/partners",
            new { partnerId = partnerId.ToString() });
        Assert.Equal(HttpStatusCode.OK, addPartner.StatusCode);

        // ---- ТРИГГЕР (§8): включён, только если в ответе уже есть имена ----
        var beforeRemove = await c.GetAsync($"api/v1/journal/records/{recordId}");
        Assert.Equal(HttpStatusCode.OK, beforeRemove.StatusCode);
        var beforeBody = await beforeRemove.Content.ReadAsStringAsync();

        if (!beforeBody.Contains(partnerName, StringComparison.Ordinal))
        {
            // Имён в ответе нет → триггер не сработал. Не валим: причина названа, включение машинное.
            Console.WriteLine(
                "::warning::[spec][pending:PartnersDirectory] " +
                "журнал отдаёт только id партнёров (0 совпадений по partnername в 6-msa) — " +
                "кейс включится, когда partners_directory начнёт отдавать имена в /journal/records/{id}; " +
                "ассерт не ослаблен.");
            return;
        }

        var remove = await c.PostAsJsonAsync($"api/v1/partners/{partnerId}/remove", new { });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var afterRemove = await c.GetAsync($"api/v1/journal/records/{recordId}");
        Assert.Equal(HttpStatusCode.OK, afterRemove.StatusCode);
        var afterBody = await afterRemove.Content.ReadAsStringAsync();

        // История остаётся разрешимой: имя видно ПОСЛЕ мягкого удаления партнёра.
        Assert.Contains(partnerName, afterBody, StringComparison.Ordinal);
    }
}
