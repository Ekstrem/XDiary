using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>TE-009: E2E соматика + аналитика (срез, сводка, корреляции).</summary>
public class SomaticAnalyticsApiTests
{
    [ApiFact]
    public async Task SomaticSlice_ReturnsData()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        // Известная рассинхронизация: шлюз роутит /api/v1/somatic-series/**, а контроллер сервиса — /api/v1/somatic.
        // Пробуем оба варианта — тест станет зелёным, когда любую из сторон выровняют.
        var paths = new[]
        {
            "api/v1/somatic/slice?fromT=2026-10-29T20:00&toT=2026-10-29T22:00",
            "api/v1/somatic-series/slice?fromT=2026-10-29T20:00&toT=2026-10-29T22:00",
        };

        // Диагностика: печатаем статус И тело каждого пути — иначе в аннотации CI
        // видно только «не сработал», и причина (404 маршрута vs 401 vs 400 формата) неясна.
        var diag = new List<string>();
        foreach (var p in paths)
        {
            var resp = await c.GetAsync(p);
            if (resp.IsSuccessStatusCode) return;
            var body = await resp.Content.ReadAsStringAsync();
            if (body.Length > 300) body = body.Substring(0, 300);
            diag.Add($"{p} -> {(int)resp.StatusCode}: {body.Replace("\n", " ")}");
        }

        Assert.Fail("Ни один путь соматики не сработал:\n" + string.Join("\n", diag));
    }

    [ApiFact]
    public async Task AnalyticsSummary_ReturnsCards()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        // Контракт: AnalyticsController — `[HttpGet("summary")]` с `[FromQuery] period`.
        // Раньше тест шёл POST'ом (legacy-привычка) и получал 405; тест был в `skip`,
        // поэтому дефект не был виден до TE-041.
        var summary = await E2EInfra.GetAsync(c, "api/v1/analytics/summary?period=30");
        Assert.True(summary.TryGetProperty("sessions", out _), "в сводке есть sessions");
        Assert.True(summary.TryGetProperty("orgasms", out _), "в сводке есть orgasms");
    }

    [ApiFact]
    public async Task AnalyticsCorrelations_ReturnsList()
    {
        var c = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, session);

        // Контракт: `[HttpGet("correlations")]` без тела; POST /analytics/* объявлен только
        // для `events`, поэтому POST давал 405.
        var corr = await E2EInfra.GetAsync(c, "api/v1/analytics/correlations");
        Assert.True(corr.ValueKind == JsonValueKind.Array || corr.ValueKind == JsonValueKind.Object);
    }
}
