using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Journal.IntegrationTests;
using Journal.Storage;
using Xunit;

public class JournalIntegrationTest
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public void Command_Then_Query_ReturnsRecord()
    {
        var aggregate = Aggregate.CreateInstance(new AnemicModel());
        var start = aggregate.StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00", Cmd("SpecifyWhen"));
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1", Cmd("AddPartner"));
        var complete = Aggregate.CreateInstance(partner.Model()).Complete(Cmd("Complete"));
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);

        var readModel = JournalReadModelBuilder.ToReadModel(complete.Model());
        Assert.Equal(new[] { "partner-1" }, readModel.Partners);
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T21:00"), DateTimeOffset.Parse(readModel.StartedAt));
    }
}
