using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain.Implementation;
using Xunit;

public class PartnerAggregateTest
{
    [Fact]
    public void Register_Then_Rename_Then_Remove()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice");
        Assert.Equal(DomainOperationResultEnum.Success, start.Result);
        var rename = Aggregate.CreateInstance(start.Model()).Rename("Bob");
        Assert.Equal(DomainOperationResultEnum.Success, rename.Result);
        var remove = Aggregate.CreateInstance(rename.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, remove.Result);
    }

    [Fact]
    public void Register_WithoutName_Fails()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).Register("");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateProfile_BadGender_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice");
        var result = Aggregate.CreateInstance(start.Model()).UpdateProfile("photo", "x", "note");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}