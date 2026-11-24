using System.Reflection;
using DigiTFactory.Libraries.Testing;
using AppleHealthGateway.Domain.Implementation;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата AppleImport обязан иметь тест (Verify + VerifyInvariants).
public class AppleImportCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }
}
