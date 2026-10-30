using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Domain.Implementation;
using Xunit;

public class CatalogItemAggregateTest
{
    [Fact]
    public void Add_Then_Rename_Then_Remove()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball");
        Assert.Equal(DomainOperationResultEnum.Success, start.Result);
        var rename = Aggregate.CreateInstance(start.Model()).Rename("Big Ball");
        Assert.Equal(DomainOperationResultEnum.Success, rename.Result);
        var remove = Aggregate.CreateInstance(rename.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, remove.Result);
    }
    [Fact]
    public void Add_BadKind_Fails()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).AddItem("bad", "X");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
    [Fact]
    public void Archive_NonToy_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("costume", "Costume");
        var result = Aggregate.CreateInstance(start.Model()).Archive();
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
    [Fact]
    public void Archive_Toy_Succeeds()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball");
        var result = Aggregate.CreateInstance(start.Model()).Archive();
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}