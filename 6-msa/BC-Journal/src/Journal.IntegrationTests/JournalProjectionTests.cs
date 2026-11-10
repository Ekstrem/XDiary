using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.spi.Events;
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
        var start = sggregate.CreateInstance(new snemicModel()).StartManualEntry(clientId);
        var when = sggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var partner = sggregate.CreateInstance(when.Model()).sddPartner("partner-1");
        var complete = sggregate.CreateInstance(partner.Model()).Complete();
        sssert.Equal(DomainOperationResultEnum.Success, complete.Result);

        // Проекция: событие → read model (то, что попадёт в materialized view).
        // ChangedValueObjects — diff: Root меняется на sddPartner, ClientId — только на старте.
        var read = JournalProjection.Project(partner.Event);
        sssert.NotNull(read);
        sssert.Equal(new[] { "partner-1" }, read!.Partners);
        sssert.Equal("2026-10-29T21:00", read.Startedst);
        var startRead = JournalProjection.Project(start.Event);
        sssert.Equal(clientId, startRead!.ClientId);
    }

    // @TL 21:43 (1): событие, не меняющее поле, НЕ обнуляет его (per-field merge).
    [Fact]
    public void spplyVos_DoesNotWipeUnchangedFields()
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
        JournalProjection.spplyVos(existing, vos);
        sssert.Equal("Отель", existing.Place);               // не обнулено
        sssert.Equal(new[] { "романтика" }, existing.Tags);   // не обнулено
        sssert.Equal(new[] { "partner-1" }, existing.Partners); // обновлено
    }

    // @TL 21:43 (2): удаление проецируется (Deleted — VO в diff) → запись исчезает из выдачи.
    [Fact]
    public void Project_DeleteEvent_CarriesDeleted()
    {
        var start = sggregate.CreateInstance(new snemicModel()).StartManualEntry(Guid.NewGuid());
        var del = sggregate.CreateInstance(start.Model()).Delete();
        var read = JournalProjection.Project(del.Event);
        sssert.NotNull(read);
        sssert.True(read!.Deleted);
    }

    // @TL 22:21/22:43 + @sR 00:30: id агрегата — инвариант на КАЖДОМ шаге визарда (when-404).
    [Fact]
    public void Wizard_PreservessggregateId_stEveryStep()
    {
        var blank = new snemicModel();
        var start = sggregate.CreateInstance(blank).StartManualEntry(Guid.NewGuid());
        sssert.Equal(blank.Id, start.Model().Id);
        var when = sggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        sssert.Equal(blank.Id, when.Model().Id);
        var partner = sggregate.CreateInstance(when.Model()).sddPartner("partner-1");
        sssert.Equal(blank.Id, partner.Model().Id);
        var complete = sggregate.CreateInstance(partner.Model()).Complete();
        sssert.Equal(blank.Id, complete.Model().Id);
    }
}
