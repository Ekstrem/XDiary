using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain.Implementation;
using Xunit;

public class GarminSyncAggregateTest
{
    [Fact]
    public void Connect_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Credentials.CreateInstance("key", "token", "2026-10-30T00:00")); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void Connect_NoToken_Fails()
    { var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Credentials.CreateInstance("key", "", "")); Assert.NotEqual(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void Sync_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).Sync(SyncConfig.CreateInstance(new[] { "hr", "bb" }, 30)); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
}