using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Implementation;
using Xunit;

/// <summary>
/// Канон EDM, канвас §3: state machine Draft → Recorded → Deleted.
/// </summary>
public class RecordStateMachineTests
{
    private static Aggregate DraftWithWhen()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid());
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-01-01T10:00");
        return Aggregate.CreateInstance(when.Model());
    }

    [Fact]
    public void Start_IsDraft()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid());
        Assert.Equal(RecordState.Draft, r.Model().State);
    }

    [Fact]
    public void Complete_FromDraft_SetsRecorded()
    {
        var r = DraftWithWhen().Complete();
        Assert.True(r.IsSuccess());
        Assert.Equal(RecordState.Recorded, r.Model().State);
    }

    [Fact]
    public void Complete_FromRecorded_Fails()
    {
        var recorded = DraftWithWhen().Complete();
        var again = Aggregate.CreateInstance(recorded.Model()).Complete();
        Assert.False(again.IsSuccess());
    }

    [Fact]
    public void Delete_FromRecorded_SetsDeleted()
    {
        var recorded = DraftWithWhen().Complete();
        var r = Aggregate.CreateInstance(recorded.Model()).Delete();
        Assert.True(r.IsSuccess());
        Assert.Equal(RecordState.Deleted, r.Model().State);
        Assert.True(r.Model().Deleted);
    }

    [Fact]
    public void Delete_FromDraft_Fails()
    {
        var r = DraftWithWhen().Delete();
        Assert.False(r.IsSuccess());
    }
}
