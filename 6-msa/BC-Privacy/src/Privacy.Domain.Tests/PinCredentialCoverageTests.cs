using System.Reflection;
using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using Privacy.Domain.Implementation;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата PinCredential обязан иметь тест (Verify + VerifyInvariants).
public class PinCredentialCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- ChangePin ----
    [Fact]
    public void ChangePin_WhenPinSet_ShouldSucceed()
    {
        var set = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), "salt", "hash", 100000);
        var result = Aggregate.CreateInstance(set.Model()).ChangePin("salt2", "hash2", 100000);
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- Remove ----
    [Fact]
    public void Remove_WhenPinSet_ShouldSucceed()
    {
        var set = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), "salt", "hash", 100000);
        var result = Aggregate.CreateInstance(set.Model()).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}
