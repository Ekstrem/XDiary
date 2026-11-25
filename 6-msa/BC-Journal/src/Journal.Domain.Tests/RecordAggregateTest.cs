using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Xunit;

public class RecordAggregateTest
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public void StartManualEntry_PersistsClientId()
    {
        var clientId = Guid.NewGuid();
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(clientId, Cmd("StartManualEntry"));
        Assert.Equal(clientId, start.Model().ClientId);
    }

    [Fact]
    public void Complete_WithoutStartedAt_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var result = Aggregate.CreateInstance(start.Model()).Complete(Cmd("Complete"));
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Wizard_Then_Complete_Succeeds()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen"));
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1", Cmd("AddPartner"));
        var complete = Aggregate.CreateInstance(partner.Model()).Complete(Cmd("Complete"));
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);
    }

    [Fact]
    public void Solo_Complete_Succeeds()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen"));
        var complete = Aggregate.CreateInstance(when.Model()).Complete(Cmd("Complete"));
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);
    }
}
