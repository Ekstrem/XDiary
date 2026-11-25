using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.IntegrationTests.Api;
using Journal.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Journal.IntegrationTests.Projections;

/// <summary>BE-151: после очистки Read Store RebuildAsync восстанавливает записи и регистры из Event Store.</summary>
public sealed class RebuildProjectionTests : JournalApiTestBase
{
    public RebuildProjectionTests(JournalApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Rebuild_When_ReadStoreIsEmpty_Should_RestoreRecordsAndRegisters()
    {
        if (Unavailable) return;
        var c = Fixture.CreateClient();
        var first = (await c.StartManualAsync()).EnsureOk().Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyPlaceAsync(first, "Rebuild дом")).EnsureOk("first/place");
        (await c.AddAccessoriesAsync(first, Array.Empty<string>(), Array.Empty<string>(), new[] { "Rebuild фантазия" }, Array.Empty<string>())).EnsureOk("first/accessories");
        (await c.SpecifyWhenAsync(first, "2026-10-29T21:00")).EnsureOk("first/when");
        (await c.CompleteAsync(first)).EnsureOk("first/complete");

        var second = (await c.StartManualAsync()).EnsureOk("second/start").Json().RootElement.GetProperty("id").GetGuid();
        (await c.SpecifyWhenAsync(second, "2026-10-30T21:00")).EnsureOk("second/when");
        (await c.CompleteAsync(second)).EnsureOk("second/complete");
        (await c.DeleteAsync(second)).EnsureOk("second/delete");

        Assert.True(await Fixture.WaitForAsync(async () => (await Fixture.AllRecordsAsync()).Count == 2));
        await Fixture.ClearReadStoreAsync();
        Assert.Empty(await Fixture.AllRecordsAsync());

        using var scope = Fixture.Factory.Services.CreateScope();
        var rebuild = scope.ServiceProvider.GetRequiredService<IRebuildService<IJournal>>();
        await rebuild.RebuildAsync();

        var restored = await Fixture.AllRecordsAsync();
        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, r => r.Id == first && r.Place == "Rebuild дом");
        Assert.Contains(restored, r => r.Id == second && r.Deleted);
        var defaults = await Fixture.ReadDefaultsAsync(c.ClientId);
        Assert.NotNull(defaults);
        Assert.Contains("Rebuild дом", defaults!.Places);
    }
}
