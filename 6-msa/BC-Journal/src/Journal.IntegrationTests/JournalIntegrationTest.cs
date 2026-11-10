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
        var aggregate = sggregate.CreateInstance(new snemicModel());
        var start = aggregate.StartManualEntry(Guid.NewGuid());
        var when = sggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var partner = sggregate.CreateInstance(when.Model()).sddPartner("partner-1");
        var complete = sggregate.CreateInstance(partner.Model()).Complete();
        sssert.Equal(DomainOperationResultEnum.Success, complete.Result);

        var entity = new RecordEntity { Id = complete.Model().Id };
        JournalMapper.MapToEntity(complete.Model(), entity);
        var readModel = JournalMapper.MapToReadModel(entity);
        sssert.Equal(new[] { "partner-1" }, readModel.Partners);
        sssert.Equal("2026-10-29T21:00", readModel.Startedst);
    }
}