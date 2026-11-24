using System.Reflection;
using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using Partners.Domain.Implementation;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата Partner обязан иметь тест (Verify + VerifyInvariants).
public class PartnerCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- LinkClient ----
    [Fact]
    public void LinkClient_WhenRegistered_ShouldSetClientId()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        var clientId = Guid.NewGuid();
        var result = Aggregate.CreateInstance(start.Model()).LinkClient(clientId);
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal(clientId, result.Model().ClientId);
    }

    // ---- UnlinkClient ----
    [Fact]
    public void UnlinkClient_WhenLinked_ShouldClearClientId()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice", Guid.NewGuid());
        var linked = Aggregate.CreateInstance(start.Model()).LinkClient(Guid.NewGuid());
        var result = Aggregate.CreateInstance(linked.Model()).UnlinkClient();
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Null(result.Model().ClientId);
    }
}
