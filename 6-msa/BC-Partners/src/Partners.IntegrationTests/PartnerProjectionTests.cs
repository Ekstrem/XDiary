using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Api.Events;
using Partners.Domain.Implementation;
using Xunit;

/// <summary>
/// Проверка проекции события → materialized view (read model) на моках:
/// команда исполняется агрегатом, событие проецируется PartnerProjection.Project.
/// </summary>
public class PartnerProjectionTests
{
    [Fact]
    public void Project_FromRegisterEvent_BuildsReadModel()
    {
        var clientId = Guid.NewGuid();
        // Команда: регистрация партнёра с именем + привязкой клиента.
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);
        Assert.Equal(DomainOperationResultEnum.Success, reg.Result);

        // Проекция: событие → read model.
        var read = PartnerProjection.Project(reg.Event);
        Assert.NotNull(read);
        Assert.Equal("Алиса", read!.Name);
        Assert.Equal(clientId, read.ClientId);
        Assert.Null(read.DeletedAt);
    }
}
