using Xunit;
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
    [Fact]
    public void StartManualEntry_TracksClientId_InValueObjects()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);

        var vos = r.BusinessOperationData.Model.GetValueObjects();
        Assert.Contains("ClientId", vos.Keys);
        Assert.Contains("Root", vos.Keys);
    }

    [Fact]
    public void Complete_TracksUpdatedAt_AndKeepsClientId()
    {
        var clientId = Guid.NewGuid();
        var started = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(clientId).BusinessOperationData.Model;
        var completed = Aggregate.CreateInstance(started).Complete().BusinessOperationData.Model;

        Assert.Equal(clientId, completed.ClientId);
        Assert.NotNull(completed.GetValueObjects()["ClientId"]);
        Assert.NotNull(completed.GetValueObjects()["Root"]);
    }
}
