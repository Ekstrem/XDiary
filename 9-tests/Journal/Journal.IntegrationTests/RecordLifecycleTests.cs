using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using Journal.Storage;
using Xunit;

namespace XDiary.Tests.Journal;

/// <summary>TE-001: полный жизненный цикл Record (Command → Query → Assert).</summary>
public class RecordLifecycleTests
{
    private static IJournalAnemicModel Step(IJournalAnemicModel model, Func<Aggregate, AggregateResult<IJournal, IJournalAnemicModel>> command)
    {
        var result = command(Aggregate.CreateInstance(model));
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        return result.Model();
    }

    private static IJournalAnemicModel StartManual()
        => Step(new AnemicModel(), a => a.StartManualEntry());

    private static JournalReadModel ToReadModel(IJournalAnemicModel model)
    {
        var entity = new RecordEntity();
        JournalMapper.MapToEntity(model, entity);
        return JournalMapper.MapToReadModel(entity);
    }

    [Fact]
    public void Full_Lifecycle_Command_Then_Query_ReturnsAllFields()
    {
        var model = StartManual();
        model = Step(model, a => a.SpecifyWhen("2026-10-29T21:00"));
        model = Step(model, a => a.AddPartner("partner-1"));
        model = Step(model, a => a.SpecifyPlace("дома"));
        model = Step(model, a => a.AddPositions(new[] { "Миссионерская", "Догги (сзади)" }));
        model = Step(model, a => a.AddAccessories(
            new[] { "костюм медсестры" }, new[] { "вибратор" }, new[] { "ролевая игра" }, new[] { "романтика" }));
        model = Step(model, a => a.RecordContraception(new[] { "презерватив" }, 2));
        model = Step(model, a => a.SpecifyDuration(25));
        model = Step(model, a => a.RecordOrgasms(new[]
        {
            Orgasms.CreateInstance("me", 1, new[] { "обычный" }, 5),
            Orgasms.CreateInstance("partner-1", 2, new[] { "клиторальный", "вагинальный" }, 4)
        }));
        model = Step(model, a => a.Complete());

        var read = ToReadModel(model);

        Assert.Equal("2026-10-29T21:00", read.StartedAt);
        Assert.Equal(new[] { "partner-1" }, read.Partners);
        Assert.Equal("дома", read.Place);
        Assert.Equal(new[] { "Миссионерская", "Догги (сзади)" }, read.Positions);
        Assert.Equal(new[] { "костюм медсестры" }, read.Costumes);
        Assert.Equal(new[] { "вибратор" }, read.Toys);
        Assert.Equal(new[] { "ролевая игра" }, read.Fantasies);
        Assert.Equal(new[] { "романтика" }, read.Tags);
        Assert.Equal(new[] { "презерватив" }, read.Contraceptives);
        Assert.Equal(2, read.ContraceptiveCount);
        Assert.Equal(25, read.DurationMin);
        Assert.False(read.Garmin);
        Assert.Equal(2, read.Orgasms.Count);
        Assert.Equal("me", read.Orgasms[0].Participant);
        Assert.Equal(5, read.Orgasms[0].Rating);
        Assert.Equal("partner-1", read.Orgasms[1].Participant);
        Assert.Equal(new[] { "клиторальный", "вагинальный" }, read.Orgasms[1].Types);
    }

    [Fact]
    public void StartFromGarmin_Then_Complete_PreservesOrigin()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartFromGarmin("2026-10-19T20:02", 18, "garmin-24790291519");
        Assert.Equal(DomainOperationResultEnum.Success, start.Result);
        var model = Step(start.Model(), a => a.AddPartner("partner-1"));
        model = Step(model, a => a.Complete());

        var read = ToReadModel(model);
        Assert.True(read.Garmin);
        Assert.Equal("garmin-24790291519", read.GarminId);
        Assert.Equal(18, read.DurationMin);
        Assert.Equal("2026-10-19T20:02", read.StartedAt);
    }

    [Fact]
    public void Query_ReadModel_DoesNotTouch_EventStore()
    {
        // Query читает Read Store (entity → read model), а не Event Store.
        var model = StartManual();
        model = Step(model, a => a.SpecifyWhen("2026-10-29T21:00"));
        model = Step(model, a => a.AddPositions(new[] { "Ложки" }));

        var entity = new RecordEntity();
        JournalMapper.MapToEntity(model, entity);
        var read = JournalMapper.MapToReadModel(entity);

        Assert.Equal("2026-10-29T21:00", read.StartedAt);
        Assert.Equal(new[] { "Ложки" }, read.Positions);
        // Round-trip entity → model сохраняет данные (без Event Store).
        var roundTrip = JournalMapper.MapToModel(entity);
        Assert.Equal(model.Root.StartedAt, roundTrip.Root.StartedAt);
    }
}
