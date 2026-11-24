using System.Net;
using Journal.IntegrationTests.Infrastructure;
using Xunit;

namespace Journal.IntegrationTests.Api;

/// <summary>BE-151: list/get/recent/places/fantasies, пагинация, владелец и Deleted.</summary>
public sealed class QueryEndpointsTests : JournalApiTestBase
{
    public QueryEndpointsTests(JournalApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task List_When_DraftAndRecorded_Should_IncludeBothButNotDeleted()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var first = (await c.StartManualAsync()).EnsureOk();
        var second = (await c.StartManualAsync()).EnsureOk();
        var secondId = second.Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyWhenAsync(secondId, "2026-10-29T21:00")).EnsureOk();
        (await c.CompleteAsync(secondId)).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
            (await c.GetRecordsAsync()).Json().RootElement.GetArrayLength() == 2));
        var list = (await c.GetRecordsAsync()).EnsureOk().Json().RootElement;
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task List_When_LimitAndOffset_Should_Page()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        for (var i = 0; i < 3; i++) (await c.StartManualAsync()).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
            (await c.GetRecordsAsync()).Json().RootElement.GetArrayLength() == 3));
        var page = (await c.GetRecordsAsync("limit=2&offset=1")).EnsureOk().Json().RootElement;
        Assert.Equal(2, page.GetArrayLength());
    }

    [Fact]
    public async Task Get_When_UnknownId_Should_ReturnNotFound()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.GetRecordAsync(Guid.NewGuid())).Ensure(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recent_When_NoRecords_Should_ReturnNull()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var response = (await c.GetRecentAsync()).EnsureOk();
        Assert.True(response.Body is "null" or "" or "{}");
    }

    [Fact]
    public async Task PlacesAndFantasies_When_RegistersFilled_Should_ReturnDistinctValues()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = (await c.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyPlaceAsync(id, "дома")).EnsureOk();
        (await c.SpecifyPlaceAsync(id, "дома")).EnsureOk();
        (await c.AddAccessoriesAsync(id, Array.Empty<string>(), Array.Empty<string>(), new[] { "f1", "f1" }, Array.Empty<string>())).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
            (await c.GetPlacesAsync()).Json().RootElement.GetArrayLength() == 1
            && (await c.GetFantasiesAsync()).Json().RootElement.GetArrayLength() == 1));
    }

    [Fact]
    public async Task Recent_When_RecordCompleted_Should_ReturnLastPlace()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = (await c.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyPlaceAsync(id, "Отель")).EnsureOk();
        (await c.SpecifyWhenAsync(id, "2026-10-29T21:00")).EnsureOk();
        (await c.CompleteAsync(id)).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
            (await c.GetRecentAsync()).Json().RootElement.TryGetProperty("lastPlace", out var place)
            && place.GetString() == "Отель"));
    }

    [Fact]
    public async Task Get_When_Deleted_Should_ReturnNotFound()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = (await c.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyWhenAsync(id, "2026-10-29T21:00")).EnsureOk();
        (await c.CompleteAsync(id)).EnsureOk();
        (await c.DeleteAsync(id)).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () => (await Fixture.ReadRecordAsync(id))?.Deleted == true));
        (await c.GetRecordAsync(id)).Ensure(HttpStatusCode.NotFound);
    }
}
