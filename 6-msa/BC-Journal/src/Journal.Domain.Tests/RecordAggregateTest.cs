using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Xunit;

namespace Journal.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата Record (metadata-driven, визард, in-memory).
/// </summary>
public class RecordAggregateTest
{
    [Fact]
    public void StartManualEntry_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);
        var model = RecordBuilder.NewDraftModel(id);

        var result = aggregate.StartManualEntry(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void StartManualEntry_AlreadyExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.NewDraftModel(id);

        // Инвариант IsNewEntity должен провалиться
        var result = aggregate.StartManualEntry(model, model.CommandName.CreateCommandMetadata());

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void SpecifyWhen_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.Model(id, 1, "SpecifyWhen", RecordBuilder.WhenVos("2026-10-29T21:00"));

        var result = aggregate.SpecifyWhen(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void SpecifyWhen_OnNew_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);
        var model = RecordBuilder.Model(id, 0, "SpecifyWhen", RecordBuilder.WhenVos("2026-10-29T21:00"));

        // Инвариант IsNotNewEntity должен провалиться
        var result = aggregate.SpecifyWhen(model, model.CommandName.CreateCommandMetadata());

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Complete_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.Model(id, 1, "Complete", RecordBuilder.CompleteVos("partner-1", "2026-10-29T21:00"));

        var result = aggregate.Complete(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Complete_MissingPartner_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.Model(id, 1, "Complete", RecordBuilder.CompleteVos("", "2026-10-29T21:00"));

        // Инвариант PropertyNotNull:Root.PartnerId должен провалиться
        var result = aggregate.Complete(model, model.CommandName.CreateCommandMetadata());

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Delete_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.Model(id, 1, "Delete", new Dictionary<string, IValueObject>());

        var result = aggregate.Delete(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}
