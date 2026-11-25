using System.Reflection;
using DigiTFactory.Libraries.SeedWorks.Events;
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
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

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
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.NotEqual(default, result.Model().Root.StartedAt);
    }

    [Fact]
    public void SpecifyWhen_InvalidIso_Fails()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen("не дата", Cmd("SpecifyWhen"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- SpecifyDuration ----
    [Fact]
    public void SpecifyDuration_Positive_SetsDuration()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyDuration(25, Cmd("SpecifyDuration"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal(25, result.Model().DurationMin);
    }

    // ---- RecordOrgasms ----
    [Fact]
    public void RecordOrgasms_ValidRating_SetsOrgasms()
    {
        var orgasms = new[] { Journal.Domain.Implementation.Orgasms.CreateInstance("me", 1, new[] { "kind" }, 5) };
        var result = Aggregate.CreateInstance(StartManual()).RecordOrgasms(orgasms, Cmd("RecordOrgasms"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Single(result.Model().Orgasms);
    }

    // ---- DurationPositiveValidator ----
    [Fact]
    public void DurationPositiveValidator_WhenZero_ShouldFail()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyDuration(0, Cmd("SpecifyDuration"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RatingRangeValidator ----
    [Fact]
    public void RatingRangeValidator_WhenRatingOutOfRange_ShouldFail()
    {
        var orgasms = new[] { Journal.Domain.Implementation.Orgasms.CreateInstance("me", 1, new[] { "kind" }, 9) };
        var result = Aggregate.CreateInstance(StartManual()).RecordOrgasms(orgasms, Cmd("RecordOrgasms"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordIsDraftValidator ----
    [Fact]
    public void RecordIsDraftValidator_WhenCompleted_ShouldFailMutation()
    {
        var result = Aggregate.CreateInstance(Recorded()).AddPartner("partner-9", Cmd("AddPartner"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- WhenRequiredValidator ----
    [Fact]
    public void WhenRequiredValidator_WhenStartedAtMissing_ShouldFailComplete()
    {
        var result = Aggregate.CreateInstance(StartManual()).Complete(Cmd("Complete"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordNotDeletedValidator ----
    [Fact]
    public void RecordNotDeletedValidator_WhenDeleted_ShouldFailAnyOperation()
    {
        var deleted = Aggregate.CreateInstance(Recorded()).Delete(Cmd("Delete")).Model();
        var result = Aggregate.CreateInstance(deleted).SpecifyPlace("дома", Cmd("SpecifyPlace"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- RecordDeletableValidator ----
    [Fact]
    public void RecordDeletableValidator_WhenDraft_ShouldFailDelete()
    {
        var result = Aggregate.CreateInstance(StartManual()).Delete(Cmd("Delete"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    private static IJournalAnemicModel StartManual()
        => Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry")).Model();

    private static IJournalAnemicModel Recorded()
    {
        var withWhen = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen")).Model();
        return Aggregate.CreateInstance(withWhen).Complete(Cmd("Complete")).Model();
    }
}
