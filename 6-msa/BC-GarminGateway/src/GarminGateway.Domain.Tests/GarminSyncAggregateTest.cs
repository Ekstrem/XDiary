using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using Xunit;

namespace GarminGateway.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата GarminSync (metadata-driven, in-memory).
/// </summary>
public class GarminSyncAggregateTest
{
    [Fact]
    public void UpsertSyncState_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = GarminGatewayMetadataHelper.CreateNewAggregate(id);
        var model = GarminSyncBuilder.NewSyncModel(id);

        var result = aggregate.UpsertSyncState(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertSyncState_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = GarminGatewayMetadataHelper.CreateAggregate(GarminSyncBuilder.ExistingSyncModel(id));
        var model = GarminSyncBuilder.NewSyncModel(id);

        // Upsert: повторное сохранение состояния не должно падать (нет IsNewEntity)
        var result = aggregate.UpsertSyncState(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpsertSyncState_MissingSourceId_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = GarminGatewayMetadataHelper.CreateNewAggregate(id);

        var vos = GarminSyncBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("GarminSyncRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = null
        });
        var model = DynamicAnemicModel<IGarminGateway>.Create(id, 0, Guid.NewGuid(), "UpsertSyncState", "UpsertSyncState", vos);

        var result = aggregate.UpsertSyncState(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.SourceId должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
