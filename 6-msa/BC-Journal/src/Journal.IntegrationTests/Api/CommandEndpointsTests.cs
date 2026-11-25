using System.Net;
using Journal.IntegrationTests.Infrastructure;
using Xunit;

namespace Journal.IntegrationTests.Api;

/// <summary>BE-151: все 13 команд Journal API, успешные и негативные сценарии.</summary>
public sealed class CommandEndpointsTests : JournalApiTestBase
{
    public CommandEndpointsTests(JournalApiFixture fixture) : base(fixture) { }

    private static Guid Id(ApiResponse response) => response.Json().RootElement.GetProperty("id").GetGuid();

    [Fact]
    public async Task FullWizard_When_AllCommandsCalled_Should_Succeed()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.SpecifyWhenAsync(id, "2026-10-29T21:00")).EnsureOk();
        (await c.AddPartnerAsync(id, "partner-1")).EnsureOk();
        (await c.SpecifyPlaceAsync(id, "дома")).EnsureOk();
        (await c.AddPositionsAsync(id, "p1", "p2")).EnsureOk();
        (await c.AddAccessoriesAsync(id, new[] { "c" }, new[] { "t" }, new[] { "f" }, new[] { "tag" })).EnsureOk();
        (await c.RecordContraceptionAsync(id, new[] { "condom" }, 1)).EnsureOk();
        (await c.SpecifyDurationAsync(id, 25)).EnsureOk();
        (await c.RecordOrgasmsAsync(id, new { Participant = "me", Count = 1, Types = new[] { "kind" }, Rating = 5 })).EnsureOk();
        (await c.RemovePartnerAsync(id, "partner-1")).EnsureOk();
        (await c.CompleteAsync(id)).EnsureOk();
        (await c.DeleteAsync(id)).EnsureOk();
    }

    [Fact]
    public async Task StartFromGarmin_When_Valid_Should_PrefillRecord()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartFromGarminAsync("2026-10-29T21:00", 40, "g-7")).EnsureOk());
        (await c.SpecifyWhenAsync(id, "2026-10-29T21:00")).EnsureOk();
        (await c.CompleteAsync(id)).EnsureOk();
        Assert.NotNull(await Fixture.WaitForRecordAsync(id));
        var detail = (await c.GetRecordAsync(id)).EnsureOk().Json().RootElement;
        Assert.Equal("g-7", detail.GetProperty("garminId").GetString());
        Assert.Equal(40, detail.GetProperty("durationMin").GetInt32());
    }

    [Fact]
    public async Task RemovePartner_When_CalledTwice_Should_BeIdempotent()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.AddPartnerAsync(id, "partner-1")).EnsureOk();
        (await c.RemovePartnerAsync(id, "partner-1")).EnsureOk();
        (await c.RemovePartnerAsync(id, "partner-1")).EnsureOk();
    }

    [Fact]
    public async Task AddPartner_When_IdIsOpaqueString_Should_Succeed()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.AddPartnerAsync(id, "not-a-guid-partner")).EnsureOk();
    }

    [Fact]
    public async Task SpecifyDuration_When_Zero_Should_ReturnBadRequest()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.SpecifyDurationAsync(id, 0)).Ensure(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SpecifyWhen_When_NotIso_Should_ReturnBadRequest()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.SpecifyWhenAsync(id, "не дата")).Ensure(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_When_WhenMissing_Should_ReturnBadRequest()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.CompleteAsync(id)).Ensure(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Command_When_RecordMissing_Should_ReturnNotFound()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.SpecifyPlaceAsync(Guid.NewGuid(), "дом")).Ensure(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Command_When_IdNotGuid_Should_ReturnNotFound()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.PostRawAsync($"{JournalClient.RecordsRoute}/not-a-guid/place", "{\"place\":\"дом\"}"))
            .Ensure(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartManual_When_SameIdProvidedTwice_Should_NotDuplicateListEntry()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Guid.NewGuid();
        (await c.StartManualAsync(id)).EnsureOk();
        (await c.StartManualAsync(id)).EnsureOk();
        Assert.NotNull(await Fixture.WaitForRecordAsync(id));
        var records = (await c.GetRecordsAsync()).EnsureOk().Json().RootElement;
        Assert.Equal(1, records.GetArrayLength());
    }

    [Fact]
    public async Task Delete_When_CompletedRecordDeleted_Should_ExcludeFromList()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var id = Id((await c.StartManualAsync()).EnsureOk());
        (await c.SpecifyWhenAsync(id, "2026-10-29T21:00")).EnsureOk();
        (await c.CompleteAsync(id)).EnsureOk();
        (await c.DeleteAsync(id)).EnsureOk();
        Assert.True(await Fixture.WaitForAsync(async () =>
            (await c.GetRecordsAsync()).Json().RootElement.GetArrayLength() == 0));
    }

    [Fact]
    public async Task Commands_When_PutInsteadOfPost_Should_ReturnMethodNotAllowed()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        (await c.PutAsync(JournalClient.RecordsRoute)).Ensure(HttpStatusCode.MethodNotAllowed);
    }
}
