using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XDiary.Tests.E2E;
using Xunit;

namespace Partners.ApiTests;

/// <summary>BE-144: минимальный API-контракт Partners — защита и Register/List.</summary>
public class PartnerContractTests
{
    [ApiFact]
    public async Task List_WhenNoToken_ShouldReturn401()
    {
        using var client = E2EInfra.Client();
        var response = await client.GetAsync("api/v1/partners");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [ApiFact]
    public async Task Register_WhenNameValid_ShouldAppearInList()
    {
        using var client = E2EInfra.Client();
        var session = await E2EInfra.LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);

        var id = Guid.NewGuid();
        var registered = await client.PostAsJsonAsync("api/v1/partners", new { id, name = "E2E Partner" });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        await Task.Delay(700);
        var list = await client.GetAsync("api/v1/partners");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(doc.RootElement.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
    }
}
