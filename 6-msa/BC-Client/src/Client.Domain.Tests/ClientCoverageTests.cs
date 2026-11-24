using System.Reflection;
using Client.Domain.Implementation;
using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using Xunit;

namespace Client.Domain.Tests;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата Client обязан иметь тест (Verify + VerifyInvariants).
public class ClientCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- ReissueOtp ----
    [Fact]
    public void ReissueOtp_WhenRegistered_ShouldReplaceOtp()
    {
        var registered = Aggregate.CreateInstance(new AnemicModel()).Register("+79000000000", "123456");
        var result = Aggregate.CreateInstance(registered.Model()).ReissueOtp("654321");
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal("654321", result.Model().OtpCode);
    }
}
