using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Implementation;
using Xunit;

/// <summary>
/// Канон EDM, канвас §3: state machine Draft → Recorded → Deleted.
/// </summary>
public class RecordStateMachineTests
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private static Aggregate DraftWithWhen()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-01-01T10:00", Cmd("SpecifyWhen"));
        return Aggregate.CreateInstance(when.Model());
    }

    [Fact]
    public void Start_IsDraft()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        Assert.Equal(RecordState.Draft, r.Model().State);
    }

    [Fact]
    public void Complete_FromDraft_SetsRecorded()
    {
        var r = DraftWithWhen().Complete(Cmd("Complete"));
        Assert.True(r.IsSuccess());
        Assert.Equal(RecordState.Recorded, r.Model().State);
    }

    [Fact]
    public void Complete_FromRecorded_Fails()
    {
        var recorded = DraftWithWhen().Complete(Cmd("Complete"));
        var again = Aggregate.CreateInstance(recorded.Model()).Complete(Cmd("Complete"));
        Assert.False(again.IsSuccess());
    }

    [Fact]
    public void Delete_FromRecorded_SetsDeleted()
    {
        var recorded = DraftWithWhen().Complete(Cmd("Complete"));
        var r = Aggregate.CreateInstance(recorded.Model()).Delete(Cmd("Delete"));
        Assert.True(r.IsSuccess());
        Assert.Equal(RecordState.Deleted, r.Model().State);
        Assert.True(r.Model().Deleted);
    }

    [Fact]
    public void Delete_FromDraft_Fails()
    {
        var r = DraftWithWhen().Delete(Cmd("Delete"));
        Assert.False(r.IsSuccess());
    }
}
