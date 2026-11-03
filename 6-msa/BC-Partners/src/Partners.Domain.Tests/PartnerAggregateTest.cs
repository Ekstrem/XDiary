using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain.Implementation;
using Xunit;

public class PartnerAggregateTest
{
    [Fact]
    public void Register_Then_Rename_Then_Remove()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, start.Result);
        var rename = Aggregate.CreateInstance(start.Model()).Rename("Bob");
        Assert.Equal(DomainOperationResultEnum.Success, rename.Result);
        var remove = Aggregate.CreateInstance(rename.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, remove.Result);
    }

    [Fact]
    public void Register_WithoutName_Fails()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).Register("", Guid.NewGuid());
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateProfile_BadGender_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        var result = Aggregate.CreateInstance(start.Model()).UpdateProfile("photo", "x", "note");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Remove_SetsDeletedAt()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        var remove = Aggregate.CreateInstance(start.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, remove.Result);
        Assert.NotNull(remove.Model().DeletedAt);
    }

    [Fact]
    public void Remove_IsIdempotent()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        var remove1 = Aggregate.CreateInstance(start.Model()).Remove();
        var remove2 = Aggregate.CreateInstance(remove1.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, remove2.Result);
        Assert.Equal(remove1.Model().DeletedAt, remove2.Model().DeletedAt);
    }
}