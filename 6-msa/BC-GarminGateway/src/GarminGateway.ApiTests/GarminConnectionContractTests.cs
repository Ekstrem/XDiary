using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XDiary.Tests.E2E;
using Xunit;

namespace GarminGateway.ApiTests;

/// <summary>BE-144: минимальный API-контракт Garmin — защита и Connect/State без секретов.</summary>
public class GarminConnectionContractTests
{
    [ApiFact]
    public async Task State_WhenNoToken_ShouldReturn401()
    {
        using var client = E2EInfra.Client();
        var response = await client.GetAsync("api/v1/garmin/state");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [ApiFact]
    public async Task Connect_WhenCredentialsValid_ShouldReturnConnectedState()
    {
        using var client = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);

        var command = new
        {
            credentials = new
            {
                apiKey = "e2e-key",
                oauthToken = "e2e-token",
                tokenExpiry = "2999-01-01T00:00:00+00:00",
            },
        };
        var connected = await client.PostAsJsonAsync("api/v1/garmin/connect", command);
        Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
        var body = await connected.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("connected").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("requiresReauth").GetBoolean());
        Assert.DoesNotContain("e2e-key", body, StringComparison.Ordinal);
        Assert.DoesNotContain("e2e-token", body, StringComparison.Ordinal);
    }
}
