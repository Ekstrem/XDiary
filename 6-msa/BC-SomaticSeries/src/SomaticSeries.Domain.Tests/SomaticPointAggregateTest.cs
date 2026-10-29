using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using Xunit;

namespace SomaticSeries.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата SomaticPoint (metadata-driven, in-memory).
/// </summary>
public class SomaticPointAggregateTest
{
    [Fact]
    public void ImportPoint_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = SomaticSeriesMetadataHelper.CreateNewAggregate(id);
        var model = SomaticPointBuilder.NewPointModel(id);

        var result = aggregate.ImportPoint(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportPoint_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = SomaticSeriesMetadataHelper.CreateAggregate(SomaticPointBuilder.ExistingPointModel(id));
        var model = SomaticPointBuilder.NewPointModel(id);

        // Upsert: повторный импорт той же точки не должен падать (нет IsNewEntity)
        var result = aggregate.ImportPoint(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportPoint_MissingT_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = SomaticSeriesMetadataHelper.CreateNewAggregate(id);

        var vos = SomaticPointBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("SomaticPointRoot", new Dictionary<string, object?>
        {
            ["T"] = null
        });
        var model = DynamicAnemicModel<ISomaticSeries>.Create(id, 0, Guid.NewGuid(), "ImportPoint", "ImportPoint", vos);

        var result = aggregate.ImportPoint(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.T должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
