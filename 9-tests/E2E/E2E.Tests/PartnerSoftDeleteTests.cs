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
    /// История остаётся разрешимой: журнал хранит только `partnerId`, имя подтягивается из партнёров.
    /// Если удаление физическое — имя пропадёт из уже созданной записи (потеря данных в истории).
    /// </summary>
    [E2EFact]
    public async Task Remove_PartnerInExistingJournalRecord_StillResolvable()
    {
        var (c, _) = await SessionAsync();
        var partnerName = "TE-soft-hist-" + Guid.NewGuid().ToString("N")[..8];
        var partnerId = await CreatePartnerAsync(c, partnerName);

        // Запись журнала с этим партнёром. Контракт TE-038/E7: POST /journal/records {date, partnerId, ...}.
        var create = await c.PostAsJsonAsync("api/v1/journal/records", new
        {
            date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            partnerId,
            comment = "soft-delete resolvability probe"
        });
        if (create.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            // Форма записи могла измениться — это не дефект soft-delete, поэтому не валим тест,
            // а сообщаем причину (skip-эффект через вывод): истории проверить не удалось.
            Assert.Fail($"не удалось создать запись журнала ({(int)create.StatusCode}) — форма контракта изменилась, проверьте TE-038");
        }
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var recordId = doc.RootElement.GetProperty("id").GetGuid();

        var remove = await c.PostAsJsonAsync($"api/v1/partners/{partnerId}/remove", new { });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var getRecord = await c.GetAsync($"api/v1/journal/records/{recordId}");
        Assert.Equal(HttpStatusCode.OK, getRecord.StatusCode);
        var body = await getRecord.Content.ReadAsStringAsync();

        // Имя партнёра должно остаться видимым в записи (partnerName или вложенный объект партнёра).
        Assert.Contains(partnerName, body);
    }
}
