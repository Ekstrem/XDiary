using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Xunit;

namespace Portability.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата CsvDocument (metadata-driven, in-memory).
/// </summary>
public class CsvDocumentAggregateTest
{
    [Fact]
    public void UpsertDocument_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PortabilityMetadataHelper.CreateNewAggregate(id);
        var model = CsvDocumentBuilder.NewDocumentModel(id);

        var result = aggregate.UpsertDocument(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertDocument_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = PortabilityMetadataHelper.CreateAggregate(CsvDocumentBuilder.ExistingDocumentModel(id));
        var model = CsvDocumentBuilder.NewDocumentModel(id);

        // Upsert: повторное сохранение документа не должно падать (нет IsNewEntity)
        var result = aggregate.UpsertDocument(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertDocument_MissingSourceId_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PortabilityMetadataHelper.CreateNewAggregate(id);

        var vos = CsvDocumentBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("CsvDocumentRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = null
        });
        var model = DynamicAnemicModel<IPortability>.Create(id, 0, Guid.NewGuid(), "UpsertDocument", "UpsertDocument", vos);

        var result = aggregate.UpsertDocument(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.SourceId должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
