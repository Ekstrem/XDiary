using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Xunit;

namespace Journal.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата Record (metadata-driven, in-memory).
/// </summary>
public class RecordAggregateTest
{
    [Fact]
    public void CreateRecord_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);

        var result = aggregate.CreateRecord(RecordBuilder.NewRecordModel(id));

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreateRecord_AlreadyExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingRecordModel(id));

        var result = aggregate.CreateRecord(RecordBuilder.NewRecordModel(id));

        // Инвариант IsNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreateRecord_MissingStartedAt_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);

        var vos = RecordBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("RecordRoot", new Dictionary<string, object?>
        {
            ["StartedAt"] = null,
            ["DurationMin"] = 30
        });
        var model = DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "CreateRecord", "CreateRecord", vos);

        var result = aggregate.CreateRecord(model);

        // Инвариант PropertyNotNull:Root.StartedAt должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateRecord_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingRecordModel(id));
        var model = RecordBuilder.UpdateRecordModel(id);

        var result = aggregate.UpdateRecord(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateRecord_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);
        var model = RecordBuilder.UpdateRecordModel(id);

        var result = aggregate.UpdateRecord(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeleteRecord_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingRecordModel(id));
        var model = RecordBuilder.DeleteRecordModel(id);

        var result = aggregate.DeleteRecord(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeleteRecord_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);
        var model = RecordBuilder.DeleteRecordModel(id);

        var result = aggregate.DeleteRecord(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportRecord_Upsert_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateNewAggregate(id);
        var model = RecordBuilder.ImportRecordModel(id);

        var result = aggregate.ImportRecord(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportRecord_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingRecordModel(id));
        var model = RecordBuilder.ImportRecordModel(id);

        var result = aggregate.ImportRecord(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }
}
