using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Domain.Implementation;
using Xunit;

public class PinCredentialAggregateTest
{
    [Fact]
    public void SetPin_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).SetPin("salt", "hash", 100000); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void SetPin_EmptyHash_Fails()
    { var r = Aggregate.CreateInstance(new AnemicModel()).SetPin("salt", "", 100000); Assert.NotEqual(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void Lock_Then_Unlock()
    {
        var set = Aggregate.CreateInstance(new AnemicModel()).SetPin("salt", "hash", 100000);
        var locked = Aggregate.CreateInstance(set.Model()).Lock();
        Assert.True(locked.Model().Locked);
        var unlocked = Aggregate.CreateInstance(locked.Model()).Unlock();
        Assert.False(unlocked.Model().Locked);
    }
}