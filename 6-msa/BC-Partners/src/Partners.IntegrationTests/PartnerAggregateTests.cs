using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain.Implementation;
using Xunit;

/// <summary>
/// Идемпотентность команд агрегата (BE-130): повторная привязка того же clientId
/// не должна порождать дифф по ClientId (иначе empty-DIFF валит pipeline → 500).
/// </summary>
public class PartnerAggregateTests
{
    [Fact]
    public void LinkClient_SameClientId_IsIdempotentNoOp()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        Assert.Equal(DomainOperationResultEnum.Success, reg.Result);

        // Повторная привязка ТОГО ЖЕ clientId — no-op: ClientId не меняется ⇒ нет диффа.
        var linked = Aggregate.CreateInstance(reg.Model()).LinkClient(clientId);

        Assert.Equal(DomainOperationResultEnum.Success, linked.Result);
        Assert.Equal(clientId, linked.Model().ClientId);
        Assert.False(linked.Event!.ChangedValueObjects?.ContainsKey("ClientId") ?? false);
    }

    [Fact]
    public void UnlinkThenLink_ProducesClientIdDiff()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);

        var unlinked = Aggregate.CreateInstance(reg.Model()).UnlinkClient();
        Assert.Null(unlinked.Model().ClientId);

        var relinked = Aggregate.CreateInstance(unlinked.Model()).LinkClient(clientId);
        Assert.Equal(clientId, relinked.Model().ClientId);
        // Реальное изменение: дифф по ClientId есть (null → clientId).
        Assert.True(relinked.Event!.ChangedValueObjects?.ContainsKey("ClientId") ?? false);
    }
}
