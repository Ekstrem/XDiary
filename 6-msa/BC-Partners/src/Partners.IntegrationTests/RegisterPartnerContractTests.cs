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

/// <summary>
/// BE-128: уровень 3 — контрактный. Полный HTTP через WebApplicationFactory:
/// JWT(sub=GUID) → POST /partners {name} → GET /partners содержит созданного с ClientId==sub.
/// Красный при BE-128 (партнёр без clientId), зелёный после фикса.
/// </summary>
public class RegisterPartnerContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegisterPartnerContractTests(WebApplicationFactory<Program> factory)
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
    }

    [Fact]
    public async Task Register_ThenList_ContainsCreatedPartner_WithSubAsClientId()
    {
        var clientId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MakeJwt(clientId));

        var reg = await client.PostAsJsonAsync("/api/v1/partners", new { name = "actor-test-partner" });
        Assert.True(reg.IsSuccessStatusCode, await reg.Content.ReadAsStringAsync());

        using var regDoc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        var partnerId = regDoc.RootElement.GetProperty("id").GetGuid();

        // BE-128 диагностика: если clientId в POST-ответе уже пустой — потеря в HttpActorProvider (до записи).
        var cidEl = regDoc.RootElement.GetProperty("clientId");
        var regClientId = cidEl.ValueKind == JsonValueKind.Null ? Guid.Empty : cidEl.GetGuid();
        Assert.Equal(clientId, regClientId);

        var list = await client.GetFromJsonAsync<List<PartnersReadModel>>("/api/v1/partners");
        Assert.NotNull(list);

        var created = list!.FirstOrDefault(x => x.Id == partnerId);
        Assert.NotNull(created);
        Assert.Equal(clientId, created!.ClientId);
    }

    private static string MakeJwt(Guid clientId)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", clientId.ToString()) }),
            Issuer = "xdiary",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(JwtSigningKey.Resolve(null, true)),
                SecurityAlgorithms.HmacSha256)
        });
        return handler.WriteToken(token);
    }
}
