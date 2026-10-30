using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>TE-008: E2E журнал — создание записи через цепочку команд → чтение журнала/детали.</summary>
public class JournalE2ETests
{
    [E2EFact]
    public async Task CreateRecord_Then_ReadJournalAndDetail()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        // StartManualEntry → id
        var start = await E2EInfra.PostAsync(c, "api/v1/journal/records");
        var id = start.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));

        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/when", new { startedAt = "2026-10-29T21:00" });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/partners", new { partnerId = "partner-e2e" });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/place", new { place = "дома" });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/positions", new { positions = new[] { "Ложки" } });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/duration", new { durationMin = 20 });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/orgasms", new
        {
            orgasms = new[] { new { participant = "me", count = 1, types = new[] { "обычный" }, rating = 5 } }
        });
        await E2EInfra.PostAsync(c, $"api/v1/journal/records/{id}/complete");

        // Журнал содержит запись
        var list = await E2EInfra.GetAsync(c, "api/v1/journal/records");
        Assert.Contains(id!, list.GetRawText());

        // Деталь совпадает
        var detail = await E2EInfra.GetAsync(c, $"api/v1/journal/records/{id}");
        Assert.Equal("2026-10-29T21:00", detail.GetProperty("startedAt").GetString());
        Assert.Equal(20, detail.GetProperty("durationMin").GetInt32());
    }

    [E2EFact]
    public async Task InvalidDuration_GivesError()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        var start = await E2EInfra.PostAsync(c, "api/v1/journal/records");
        var id = start.GetProperty("id").GetString();

        var resp = await c.PostAsJsonAsync($"api/v1/journal/records/{id}/duration", new { durationMin = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
