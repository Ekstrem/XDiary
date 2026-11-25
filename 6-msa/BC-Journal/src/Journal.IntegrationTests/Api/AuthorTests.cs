using System.Net;
using Journal.IntegrationTests.Infrastructure;
using Xunit;

namespace Journal.IntegrationTests.Api;

/// <summary>BE-151 (ACO AuthorTests): владелец только из sub, чужие записи изолированы.</summary>
public sealed class AuthorTests : JournalApiTestBase
{
    public AuthorTests(JournalApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ForeignRecord_When_CommandsAndQueriesCalled_Should_ReturnNotFound()
    {
        if (Unavailable) return;
        var owner = Fixture.CreateClient();
        var foreign = Fixture.CreateClient();
        var id = (await owner.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();

        (await foreign.GetRecordAsync(id)).Ensure(HttpStatusCode.NotFound);
        (await foreign.SpecifyPlaceAsync(id, "чужое")).Ensure(HttpStatusCode.NotFound);
        (await foreign.CompleteAsync(id)).Ensure(HttpStatusCode.NotFound);
        (await foreign.DeleteAsync(id)).Ensure(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_When_TwoOwners_Should_ReturnOnlyOwnRecords()
    {
        if (Unavailable) return;
        var first = Fixture.CreateClient();
        var second = Fixture.CreateClient();
        var firstId = (await first.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        var secondId = (await second.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        Assert.NotNull(await Fixture.WaitForRecordAsync(firstId));
        Assert.NotNull(await Fixture.WaitForRecordAsync(secondId));

        Assert.Equal(1, (await first.GetRecordsAsync()).Json().RootElement.GetArrayLength());
        Assert.Equal(1, (await second.GetRecordsAsync()).Json().RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Queries_When_TokenMissing_Should_ReturnUnauthorized()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SendAnonymousAsync(HttpMethod.Get, JournalClient.RecordsRoute)).Ensure(HttpStatusCode.Unauthorized);
        (await c.SendAnonymousAsync(HttpMethod.Get, $"{JournalClient.JournalRoute}/recent")).Ensure(HttpStatusCode.Unauthorized);
        (await c.SendAnonymousAsync(HttpMethod.Get, $"{JournalClient.JournalRoute}/places")).Ensure(HttpStatusCode.Unauthorized);
        (await c.SendAnonymousAsync(HttpMethod.Get, $"{JournalClient.JournalRoute}/fantasies")).Ensure(HttpStatusCode.Unauthorized);
    }
}
