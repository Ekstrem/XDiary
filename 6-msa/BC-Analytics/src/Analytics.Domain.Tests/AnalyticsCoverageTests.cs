using System.Reflection;
using Analytics.Domain.Models;
using DigiTFactory.Libraries.Testing;
using Xunit;

// BE-149 / QG-702: в Analytics нет агрегата (read-only BC, только калькуляторы) —
// Verify над доменным типом без бизнес-методов проходит тривиально; вызовы обязательны по гейту.
public class AnalyticsCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<RecordInput>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(RecordInput).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }
}
