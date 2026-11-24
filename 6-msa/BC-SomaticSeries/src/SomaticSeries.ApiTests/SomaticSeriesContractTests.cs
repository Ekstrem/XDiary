using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XDiary.Tests.E2E;
using Xunit;

namespace SomaticSeries.ApiTests;

/// <summary>BE-144: минимальный API-контракт SomaticSeries — защита и RecordPoint/Slice.</summary>
public class SomaticSeriesContractTests
{
    [ApiFact]
    public async Task Slice_WhenNoToken_ShouldReturn401()
    {
        using var client = E2EInfra.Client();
        var response = await client.GetAsync("api/v1/somatic-series/slice?fromT=2026-01-01&toT=2026-01-02");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [ApiFact]
    public async Task RecordPoint_WhenValid_ShouldAppearInSlice()
    {
        using var client = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);

        const string timestamp = "2026-11-21T16:00:00+00:00";
        var recorded = await client.PostAsJsonAsync("api/v1/somatic-series/points", new { t = timestamp, hr = 60, bodyBattery = 50 });
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);

        await Task.Delay(700);
        var slice = await client.GetAsync("api/v1/somatic-series/slice?fromT=2026-11-21T00:00:00%2B00:00&toT=2026-11-22T00:00:00%2B00:00");
        Assert.Equal(HttpStatusCode.OK, slice.StatusCode);
        using var doc = JsonDocument.Parse(await slice.Content.ReadAsStringAsync());
        Assert.Contains(doc.RootElement.EnumerateArray(), item => item.GetProperty("t").GetString() == timestamp && item.GetProperty("hr").GetInt32() == 60);
    }
}
