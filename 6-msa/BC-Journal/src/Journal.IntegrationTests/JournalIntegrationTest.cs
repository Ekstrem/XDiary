using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Journal.Storage;
using Xunit;

public class JournalIntegrationTest
{
    [Fact]
    public void Command_Then_Query_ReturnsRecord()
    {
        var aggregate = Aggregate.CreateInstance(new AnemicModel());
        var start = aggregate.StartManualEntry();
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1");
        var complete = Aggregate.CreateInstance(partner.Model()).Complete();
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);

        var entity = new RecordEntity { Id = complete.Model().Id };
        JournalMapper.MapToEntity(complete.Model(), entity);
        var readModel = JournalMapper.MapToReadModel(entity);
        Assert.Equal(new[] { "partner-1" }, readModel.Partners);
        Assert.Equal("2026-10-29T21:00", readModel.StartedAt);
    }
}