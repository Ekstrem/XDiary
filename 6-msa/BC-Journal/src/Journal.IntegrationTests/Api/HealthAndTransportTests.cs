using System.Net;
using Journal.IntegrationTests.Infrastructure;
using Xunit;

namespace Journal.IntegrationTests.Api;

/// <summary>BE-151: /health, 401 на всех 18 эндпоинтах, битый токен/JSON, 405, UTF-8.</summary>
public sealed class HealthAndTransportTests : JournalApiTestBase
{
    public HealthAndTransportTests(JournalApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Health_When_Anonymous_Should_ReturnOk()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SendAnonymousAsync(HttpMethod.Get, "/health")).Ensure(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllEndpoints_When_TokenMissing_Should_ReturnUnauthorized()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Guid.NewGuid();
        var posts = new[]
        {
            JournalClient.RecordsRoute,
            $"{JournalClient.RecordsRoute}/from-garmin",
            $"{JournalClient.RecordsRoute}/{id:D}/when",
            $"{JournalClient.RecordsRoute}/{id:D}/partners",
            $"{JournalClient.RecordsRoute}/{id:D}/partners/p1/remove",
            $"{JournalClient.RecordsRoute}/{id:D}/place",
            $"{JournalClient.RecordsRoute}/{id:D}/positions",
            $"{JournalClient.RecordsRoute}/{id:D}/accessories",
            $"{JournalClient.RecordsRoute}/{id:D}/contraception",
            $"{JournalClient.RecordsRoute}/{id:D}/duration",
            $"{JournalClient.RecordsRoute}/{id:D}/orgasms",
            $"{JournalClient.RecordsRoute}/{id:D}/complete",
            $"{JournalClient.RecordsRoute}/{id:D}/delete"
        };
        foreach (var path in posts)
            (await c.SendAnonymousAsync(HttpMethod.Post, path)).Ensure(HttpStatusCode.Unauthorized);
        var queries = new[]
        {
            JournalClient.RecordsRoute,
            $"{JournalClient.RecordsRoute}/{id:D}",
            $"{JournalClient.JournalRoute}/recent",
            $"{JournalClient.JournalRoute}/places",
            $"{JournalClient.JournalRoute}/fantasies"
        };
        foreach (var path in queries)
            (await c.SendAnonymousAsync(HttpMethod.Get, path)).Ensure(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Commands_When_TokenMalformed_Should_ReturnUnauthorized()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SendWithAuthorizationAsync(HttpMethod.Post, JournalClient.RecordsRoute, "Bearer not-a-test-token"))
            .Ensure(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartManual_When_SubjectNotGuid_Should_ReturnUnauthorized()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SendWithAuthorizationAsync(HttpMethod.Post, JournalClient.RecordsRoute,
            TestAuthHandler.AuthorizationForClaim("not-a-guid"))).Ensure(HttpStatusCode.Unauthorized);
        (await c.SendWithAuthorizationAsync(HttpMethod.Post, JournalClient.RecordsRoute,
            TestAuthHandler.AuthorizationForClaim(Guid.Empty.ToString()))).Ensure(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordResource_When_HttpMethodIsDelete_Should_ReturnMethodNotAllowed()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SendAsync(HttpMethod.Delete, $"{JournalClient.RecordsRoute}/{Guid.NewGuid():D}"))
            .Ensure(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task StartFromGarmin_When_JsonMalformed_Should_ReturnValidationProblem()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.PostRawAsync($"{JournalClient.RecordsRoute}/from-garmin", "{not-json"))
            .Ensure(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SpecifyPlace_When_RussianUtf8_Should_RoundTrip()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = (await c.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyPlaceAsync(id, "Спальня")).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
        {
            var record = await Fixture.ReadRecordAsync(id);
            return record?.Place == "Спальня";
        }));
    }
}
