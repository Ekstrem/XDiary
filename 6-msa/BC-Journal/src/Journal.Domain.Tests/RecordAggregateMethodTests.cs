using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Xunit;

// Per-method юнит-тесты: каждый бизнес-метод агрегата Record покрыт прямым тестом
// (happy-path + кейс инварианта). Дополняет RecordAggregateTest и RecordInvariantTests.
public class RecordAggregateMethodTests
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private static IJournalAnemicModel Step(
        IJournalAnemicModel model,
        Func<Aggregate, AggregateResult<IJournal, IJournalAnemicModel>> command)
    {
        var result = command(Aggregate.CreateInstance(model));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        return result.Model();
    }

    private static IJournalAnemicModel StartManual()
        => Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry")).Model();

    private static IJournalAnemicModel Recorded()
        => Step(Step(StartManual(), a => a.SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen"))), a => a.Complete(Cmd("Complete")));

    // ---- StartFromGarmin ----
    [Fact]
    public void StartFromGarmin_ValidIso_PersistsOrigin()
    {
        var result = Aggregate.CreateInstance(new AnemicModel())
            .StartFromGarmin("2026-10-19T20:02:00Z", 18, "garmin-1", Guid.NewGuid(), Cmd("StartFromGarmin"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        var m = result.Model();
        Assert.True(m.Garmin);
        Assert.Equal("garmin-1", m.GarminId);
        Assert.Equal(18, m.DurationMin);
        Assert.NotEqual(default, m.Root.StartedAt);
    }

    [Fact]
    public void StartFromGarmin_InvalidIso_Fails()
    {
        var result = Aggregate.CreateInstance(new AnemicModel())
            .StartFromGarmin("не дата", 18, "garmin-1", Guid.NewGuid(), Cmd("StartFromGarmin"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- AddPartner ----
    [Fact]
    public void AddPartner_AddsPartner()
    {
        var result = Aggregate.CreateInstance(StartManual()).AddPartner("partner-1", Cmd("AddPartner"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Contains("partner-1", result.Model().Root.Partners);
    }

    [Fact]
    public void AddPartner_Duplicate_DoesNotDuplicate()
    {
        var m = Step(StartManual(), a => a.AddPartner("partner-1", Cmd("AddPartner")));
        var again = Aggregate.CreateInstance(m).AddPartner("partner-1", Cmd("AddPartner"));
        Assert.Equal(DomainOperationResultEnum.Success, again.Result);
        Assert.Single(again.Model().Root.Partners);
    }

    // ---- RemovePartner ----
    [Fact]
    public void RemovePartner_RemovesExisting()
    {
        var m = Step(Step(StartManual(), a => a.AddPartner("partner-1", Cmd("AddPartner"))), a => a.AddPartner("partner-2", Cmd("AddPartner")));
        var result = Aggregate.CreateInstance(m).RemovePartner("partner-1", Cmd("RemovePartner"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.DoesNotContain("partner-1", result.Model().Root.Partners);
        Assert.Contains("partner-2", result.Model().Root.Partners);
    }

    // ---- SpecifyPlace ----
    [Fact]
    public void SpecifyPlace_SetsPlace()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyPlace("дома", Cmd("SpecifyPlace"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal("дома", result.Model().Place);
    }

    // ---- AddPositions ----
    [Fact]
    public void AddPositions_SetsPositions()
    {
        var result = Aggregate.CreateInstance(StartManual())
            .AddPositions(new[] { "Миссионерская", "Догги (сзади)" }, Cmd("AddPositions"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal(new[] { "Миссионерская", "Догги (сзади)" }, result.Model().Positions);
    }

    // ---- AddAccessories ----
    [Fact]
    public void AddAccessories_SetsAllFour()
    {
        var result = Aggregate.CreateInstance(StartManual())
            .AddAccessories(new[] { "костюм" }, new[] { "вибратор" }, new[] { "ролевая" }, new[] { "романтика" }, Cmd("AddAccessories"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        var m = result.Model();
        Assert.Equal(new[] { "костюм" }, m.Costumes);
        Assert.Equal(new[] { "вибратор" }, m.Toys);
        Assert.Equal(new[] { "ролевая" }, m.Fantasies);
        Assert.Equal(new[] { "романтика" }, m.Tags);
    }

    // ---- RecordContraception ----
    [Fact]
    public void RecordContraception_SetsContraceptivesAndCount()
    {
        var result = Aggregate.CreateInstance(StartManual())
            .RecordContraception(new[] { "презерватив" }, 2, Cmd("RecordContraception"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        var m = result.Model();
        Assert.Equal(new[] { "презерватив" }, m.Contraceptives);
        Assert.Equal(2, m.ContraceptiveCount);
    }

    // ---- Delete ----
    [Fact]
    public void Delete_OnRecorded_Succeeds()
    {
        var result = Aggregate.CreateInstance(Recorded()).Delete(Cmd("Delete"));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.True(result.Model().Deleted);
    }

    [Fact]
    public void Delete_OnDraft_Fails()
    {
        var result = Aggregate.CreateInstance(StartManual()).Delete(Cmd("Delete"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Delete_AlreadyDeleted_Fails()
    {
        var deleted = Aggregate.CreateInstance(Recorded()).Delete(Cmd("Delete")).Model();
        var again = Aggregate.CreateInstance(deleted).Delete(Cmd("Delete"));
        Assert.NotEqual(DomainOperationResultEnum.Success, again.Result);
    }

    // ---- RecordIsDraft: мутация после Complete невозможна ----
    [Fact]
    public void SpecifyPlace_AfterComplete_Fails()
    {
        var result = Aggregate.CreateInstance(Recorded()).SpecifyPlace("дома", Cmd("SpecifyPlace"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
