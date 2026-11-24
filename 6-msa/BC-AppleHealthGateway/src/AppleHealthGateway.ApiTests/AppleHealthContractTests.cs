using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XDiary.Tests.E2E;
using Xunit;

namespace AppleHealthGateway.ApiTests;

/// <summary>BE-144: минимальный API-контракт AppleHealth — защита и успешный Configure/State.</summary>
public class AppleHealthContractTests
{
    [ApiFact]
    public async Task State_WhenNoToken_ShouldReturn401()
    {
        using var client = E2EInfra.Client();
        var response = await client.GetAsync("api/v1/apple-health/state");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [ApiFact]
    public async Task Configure_WhenMetricsValid_ShouldReturnState()
    {
        using var client = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);

        var configured = await client.PostAsJsonAsync("api/v1/apple-health/configure", new { metrics = new[] { "hr", "steps" } });
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);

        await Task.Delay(700);
        var state = await client.GetAsync("api/v1/apple-health/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        var body = await state.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("hr", doc.RootElement.GetProperty("metrics").EnumerateArray().Select(x => x.GetString()));
    }
}
