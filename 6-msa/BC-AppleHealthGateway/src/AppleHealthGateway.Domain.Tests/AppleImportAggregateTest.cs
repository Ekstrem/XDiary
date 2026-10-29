using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using Xunit;

namespace AppleHealthGateway.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата AppleImport (metadata-driven, in-memory).
/// </summary>
public class AppleImportAggregateTest
{
    [Fact]
    public void UpsertImportState_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = AppleHealthGatewayMetadataHelper.CreateNewAggregate(id);
        var model = AppleImportBuilder.NewImportModel(id);

        var result = aggregate.UpsertImportState(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertImportState_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = AppleHealthGatewayMetadataHelper.CreateAggregate(AppleImportBuilder.ExistingImportModel(id));
        var model = AppleImportBuilder.NewImportModel(id);

        // Upsert: повторное сохранение состояния не должно падать (нет IsNewEntity)
        var result = aggregate.UpsertImportState(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertImportState_MissingSourceId_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = AppleHealthGatewayMetadataHelper.CreateNewAggregate(id);

        var vos = AppleImportBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("AppleImportRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = null
        });
        var model = DynamicAnemicModel<IAppleHealthGateway>.Create(id, 0, Guid.NewGuid(), "UpsertImportState", "UpsertImportState", vos);

        var result = aggregate.UpsertImportState(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.SourceId должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
