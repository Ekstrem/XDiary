using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using Xunit;

namespace DailyMetrics.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата DailyMetrics (metadata-driven, in-memory).
/// </summary>
public class DailyMetricsAggregateTest
{
    [Fact]
    public void ImportDaily_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = DailyMetricsMetadataHelper.CreateNewAggregate(id);
        var model = DailyMetricsBuilder.NewMetricsModel(id);

        var result = aggregate.ImportDaily(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportDaily_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = DailyMetricsMetadataHelper.CreateAggregate(DailyMetricsBuilder.ExistingMetricsModel(id));
        var model = DailyMetricsBuilder.NewMetricsModel(id);

        // Upsert: повторный импорт той же даты не должен падать (нет IsNewEntity)
        var result = aggregate.ImportDaily(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void ImportDaily_MissingDate_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = DailyMetricsMetadataHelper.CreateNewAggregate(id);

        var vos = DailyMetricsBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("DailyMetricsRoot", new Dictionary<string, object?>
        {
            ["Date"] = null
        });
        var model = DynamicAnemicModel<IDailyMetrics>.Create(id, 0, Guid.NewGuid(), "ImportDaily", "ImportDaily", vos);

        var result = aggregate.ImportDaily(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.Date должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
