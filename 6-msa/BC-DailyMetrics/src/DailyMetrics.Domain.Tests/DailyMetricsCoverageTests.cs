using System.Reflection;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using DailyMetrics.Domain.Implementation;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата DailyMetrics обязан иметь тест (Verify + VerifyInvariants).
public class DailyMetricsCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- RecordRestingHr ----
    [Fact]
    public void RecordRestingHr_WhenValid_ShouldSucceed()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).RecordRestingHr("2026-10-29", 60, Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}
