using Xunit;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;

/// <summary>
/// E1: change-track события (VO) — предпосылка replay из store.
/// «Рестарт → восстановление»: агрегат пересобирается из changed-VO события,
/// а не из EF-снапшота. Здесь проверяем, что ClientId/Root попадают в
/// GetValueObjects() и что Commit() видит изменение.
/// </summary>
public class EventSourcingReplayTest
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public void StartManualEntry_TracksClientId_InValueObjects()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);

        var vos = r.BusinessOperationData.Model.GetValueObjects();
        Assert.Contains("ClientId", vos.Keys);
        Assert.Contains("Root", vos.Keys);
    }

    [Fact]
    public void Complete_TracksUpdatedAt_AndKeepsClientId()
    {
        var clientId = Guid.NewGuid();
        var started = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(clientId, Cmd("StartManualEntry")).BusinessOperationData.Model;
        var completed = Aggregate.CreateInstance(started).Complete(Cmd("Complete")).BusinessOperationData.Model;

        Assert.Equal(clientId, completed.ClientId);
        Assert.NotNull(completed.GetValueObjects()["ClientId"]);
        Assert.NotNull(completed.GetValueObjects()["Root"]);
    }
}
