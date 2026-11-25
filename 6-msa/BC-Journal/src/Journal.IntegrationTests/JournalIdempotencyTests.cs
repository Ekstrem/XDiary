using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Xunit;

/// <summary>
/// BE-051 (класс): идемпотентность создания записи журнала. Стабильный id агрегата —
/// повтор StartManualEntry с тем же id → та же read-модель (upsert), а не вторая запись.
/// </summary>
public class JournalIdempotencyTests
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public void StartManualEntry_PreservesProvidedId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        Assert.Equal(id, r.Model().Id);
    }

    [Fact]
    public void StartManualEntry_TwoSameIds_ShareAggregateIdentity()
    {
        var id = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var b = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        Assert.Equal(a.Model().Id, b.Model().Id);
    }

    [Fact]
    public void StartFromGarmin_PreservesProvidedId_AndGarminId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id })
            .StartFromGarmin("2026-01-01T00:00:00Z", 10, "garmin-1", Guid.NewGuid(), Cmd("StartFromGarmin"));
        Assert.Equal(id, r.Model().Id);
        Assert.Equal("garmin-1", r.Model().GarminId);
    }
}
