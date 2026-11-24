using System.Reflection;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.Testing;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using XDiary.Contracts;
using Xunit;

// BE-149 / QG-702: coverage-assertion — каждый бизнес-метод и каждый инвариант
// агрегата Record обязан иметь тест (Verify + VerifyInvariants).
public class RecordCoverageTests
{
    private static bool IsTest(MethodInfo m)
        => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

    [Fact]
    public void Coverage_VerifyAndVerifyInvariants_Passes()
    {
        AggregateCoverage.Verify<Aggregate>(Assembly.GetExecutingAssembly(), IsTest);
        AggregateCoverage.VerifyInvariants(typeof(Aggregate).Assembly, Assembly.GetExecutingAssembly(), IsTest);
    }

    // ---- SpecifyWhen ----
    [Fact]
    public void SpecifyWhen_ValidIso_SetsStartedAt()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00");
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.NotEqual(default, result.Model().Root.StartedAt);
    }

    [Fact]
    public void SpecifyWhen_InvalidIso_Fails()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen("не дата");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- SpecifyDuration ----
    [Fact]
    public void SpecifyDuration_Positive_SetsDuration()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyDuration(25);
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal(25, result.Model().DurationMin);
    }

    // ---- RecordOrgasms ----
    [Fact]
    public void RecordOrgasms_ValidRating_SetsOrgasms()
    {
        var orgasms = new[] { Journal.Domain.Implementation.Orgasms.CreateInstance("me", 1, new[] { "kind" }, 5) };
        var result = Aggregate.CreateInstance(StartManual()).RecordOrgasms(orgasms);
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Single(result.Model().Orgasms);
    }

    // ---- DurationPositiveValidator ----
    [Fact]
    public void DurationPositiveValidator_WhenZero_ShouldFail()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyDuration(0);
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RatingRangeValidator ----
    [Fact]
    public void RatingRangeValidator_WhenRatingOutOfRange_ShouldFail()
    {
        var orgasms = new[] { Journal.Domain.Implementation.Orgasms.CreateInstance("me", 1, new[] { "kind" }, 9) };
        var result = Aggregate.CreateInstance(StartManual()).RecordOrgasms(orgasms);
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordIsDraftValidator ----
    [Fact]
    public void RecordIsDraftValidator_WhenCompleted_ShouldFailMutation()
    {
        var result = Aggregate.CreateInstance(Recorded()).AddPartner("partner-9");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- WhenRequiredValidator ----
    [Fact]
    public void WhenRequiredValidator_WhenStartedAtMissing_ShouldFailComplete()
    {
        var result = Aggregate.CreateInstance(StartManual()).Complete();
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordNotDeletedValidator ----
    [Fact]
    public void RecordNotDeletedValidator_WhenDeleted_ShouldFailAnyOperation()
    {
        var deleted = Aggregate.CreateInstance(Recorded()).Delete().Model();
        var result = Aggregate.CreateInstance(deleted).SpecifyPlace("дома");
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordDeletableValidator ----
    [Fact]
    public void RecordDeletableValidator_WhenDraft_ShouldFailDelete()
    {
        var result = Aggregate.CreateInstance(StartManual()).Delete();
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    private static IJournalAnemicModel StartManual()
        => Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid()).Model();

    private static IJournalAnemicModel Recorded()
    {
        var withWhen = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00").Model();
        return Aggregate.CreateInstance(withWhen).Complete().Model();
    }
}
