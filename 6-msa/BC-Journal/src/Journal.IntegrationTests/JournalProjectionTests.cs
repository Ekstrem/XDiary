using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Api.Events;
using Journal.Domain.Implementation;
using Xunit;
using XDiary.Contracts;

/// <summary>
/// Проверка проекции события → materialized view (read model) на моках:
/// команда исполняется агрегатом, событие проецируется JournalProjection.Project.
/// </summary>
public class JournalProjectionTests
{
    [Fact]
    public void Project_FromCompletedEvent_BuildsReadModel()
    {
        var clientId = Guid.NewGuid();
        // Команда: старт → когда → партнёр → complete (полный визард).
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(clientId);
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1");
        var complete = Aggregate.CreateInstance(partner.Model()).Complete();
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);

        // Проекция: событие → read model (то, что попадёт в materialized view).
        var read = JournalProjection.Project(complete.Event);
        Assert.NotNull(read);
        Assert.Equal(clientId, read!.ClientId);
        Assert.Equal(new[] { "partner-1" }, read.Partners);
        Assert.Equal("2026-10-29T21:00", read.StartedAt);
    }
}
