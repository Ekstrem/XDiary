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
        // ChangedValueObjects — diff: Root меняется на AddPartner, ClientId — только на старте.
        var read = JournalProjection.Project(partner.Event);
        Assert.NotNull(read);
        Assert.Equal(new[] { "partner-1" }, read!.Partners);
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T21:00"), DateTimeOffset.Parse(read.StartedAt));
        var startRead = JournalProjection.Project(start.Event);
        Assert.Equal(clientId, startRead!.ClientId);
    }

    // @TL 21:43 (1): событие, не меняющее поле, НЕ обнуляет его (per-field merge).
    [Fact]
    public void ApplyVos_DoesNotWipeUnchangedFields()
    {
        var existing = new Journal.InternalContracts.JournalReadModel
        {
            Id = Guid.NewGuid(),
            Place = "Отель",
            Tags = new List<string> { "романтика" },
        };
        var vos = new Dictionary<string, DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IValueObject>
        {
            ["Root"] = RecordRoot.CreateInstance(new[] { "partner-1" }, "2026-10-29T21:00"),
        };
        JournalProjection.ApplyVos(existing, vos);
        Assert.Equal("Отель", existing.Place);               // не обнулено
        Assert.Equal(new[] { "романтика" }, existing.Tags);   // не обнулено
        Assert.Equal(new[] { "partner-1" }, existing.Partners); // обновлено
    }

    // @TL 21:43 (2): удаление проецируется (Deleted — VO в diff) → запись исчезает из выдачи.
    [Fact]
    public void Project_DeleteEvent_CarriesDeleted()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid());
        var del = Aggregate.CreateInstance(start.Model()).Delete();
        var read = JournalProjection.Project(del.Event);
        Assert.NotNull(read);
        Assert.True(read!.Deleted);
    }

    // @TL 22:21/22:43 + @AR 00:30: id агрегата — инвариант на КАЖДОМ шаге визарда (when-404).
    [Fact]
    public void Wizard_PreservesAggregateId_AtEveryStep()
    {
        var blank = new AnemicModel();
        var start = Aggregate.CreateInstance(blank).StartManualEntry(Guid.NewGuid());
        Assert.Equal(blank.Id, start.Model().Id);
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        Assert.Equal(blank.Id, when.Model().Id);
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1");
        Assert.Equal(blank.Id, partner.Model().Id);
        var complete = Aggregate.CreateInstance(partner.Model()).Complete();
        Assert.Equal(blank.Id, complete.Model().Id);
    }
}
