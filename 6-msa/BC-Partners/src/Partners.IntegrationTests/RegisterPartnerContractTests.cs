using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Partners.InternalContracts;
using XDiary.Contracts;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// BE-128: уровень 3 — контрактный. Полный HTTP через WebApplicationFactory:
/// JWT(sub=GUID) → POST /partners {name} → GET /partners содержит созданного с ClientId==sub.
/// Красный при BE-128 (партнёр без clientId), зелёный после фикса.
/// </summary>
public class RegisterPartnerContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public RegisterPartnerContractTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES")
                 ?? throw new InvalidOperationException("XDIARY_TEST_POSTGRES не задан");
        _factory = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CommandDb"] = cs,
                ["ConnectionStrings:ReadDb"] = cs,
                ["Jwt:SigningKey"] = "eGRpYXJ5LWRldi1zaWduaW5nLWtleS1taW5pbXVtLTMyLWJ5dGVzLTIwMjY=",
                ["Jwt:Issuer"] = "xdiary",
            })));
        _output = output;
    }

    [Fact]
    public async Task Register_ThenList_ContainsCreatedPartner_WithSubAsClientId()
    {
        var clientId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MakeJwt(clientId));

        var reg = await client.PostAsJsonAsync("/api/v1/partners", new { name = "actor-test-partner" });
        var body = await reg.Content.ReadAsStringAsync();
        _output.WriteLine($"POST status={reg.StatusCode} body={body}");

        Assert.True(reg.IsSuccessStatusCode, body);

        using var regDoc = JsonDocument.Parse(body);
        var partnerId = regDoc.RootElement.GetProperty("id").GetGuid();

        // BE-128: проекция асинхронная (fire-and-forget) — даём ей завершиться перед чтением.
        await Task.Delay(2000);

        var list = await client.GetFromJsonAsync<List<PartnersReadModel>>("/api/v1/partners");
        _output.WriteLine($"GET count={list?.Count} partnerId={partnerId}");
        Assert.NotNull(list);

        var created = list!.FirstOrDefault(x => x.Id == partnerId);
        _output.WriteLine(created is null ? "created=NULL" : $"created.ClientId={created.ClientId}");
        Assert.NotNull(created);
        Assert.Equal(clientId, created!.ClientId);
    }

    private static string MakeJwt(Guid clientId)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(JwtSigningKey.Resolve(null, true));
        var token = new JwtSecurityToken(
            issuer: "xdiary",
            claims: new[] { new Claim("sub", clientId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }
}
