using System.Reflection;
using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using Catalog.Domain.Implementation;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата CatalogItem обязан иметь тест (Verify + VerifyInvariants).
public class CatalogItemCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- AddItem ----
    [Fact]
    public void AddItem_WhenKindValid_ShouldSucceed()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- Restore ----
    [Fact]
    public void Restore_WhenArchived_ShouldSucceed()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        var archived = Aggregate.CreateInstance(start.Model()).Archive();
        var result = Aggregate.CreateInstance(archived.Model()).Restore();
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- UpdateDetails ----
    [Fact]
    public void UpdateDetails_WhenValid_ShouldSucceed()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        var result = Aggregate.CreateInstance(start.Model()).UpdateDetails("photo", "notes", "brand", "fantasy");
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}
