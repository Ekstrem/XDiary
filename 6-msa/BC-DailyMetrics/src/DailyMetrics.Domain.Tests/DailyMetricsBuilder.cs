using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;

namespace DailyMetrics.Domain.Tests;

/// <summary>
/// Строит анемичные модели DailyMetrics для тестов (аналог DailyMetricsDraft).
/// </summary>
public static class DailyMetricsBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string date = "2026-10-29",
        int stress = 30,
        int restingHr = 60,
        int hrv = 50,
        int sleepScore = 80,
        int sleepDurationMin = 480,
        int deepMin = 90,
        int lightMin = 300,
        int remMin = 70,
        int awakeMin = 20)
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("DailyMetricsRoot", new Dictionary<string, object?>
            {
                ["Date"] = date
            }),
            ["Stress"] = new DynamicValueObject("Stress", new Dictionary<string, object?>
            {
                ["Stress"] = stress
            }),
            ["RestingHR"] = new DynamicValueObject("RestingHR", new Dictionary<string, object?>
            {
                ["RestingHr"] = restingHr
            }),
            ["Hrv"] = new DynamicValueObject("Hrv", new Dictionary<string, object?>
            {
                ["Hrv"] = hrv
            }),
            ["Sleep"] = new DynamicValueObject("Sleep", new Dictionary<string, object?>
            {
                ["Score"] = sleepScore,
                ["DurationMin"] = sleepDurationMin,
                ["DeepMin"] = deepMin,
                ["LightMin"] = lightMin,
                ["RemMin"] = remMin,
                ["AwakeMin"] = awakeMin
            })
        };
    }

    public static IAnemicModel<IDailyMetrics> NewMetricsModel(Guid id)
        => DynamicAnemicModel<IDailyMetrics>.Create(id, 0, Guid.NewGuid(), "ImportDaily", "ImportDaily", BuildValueObjects());

    public static IAnemicModel<IDailyMetrics> ExistingMetricsModel(Guid id)
        => DynamicAnemicModel<IDailyMetrics>.Create(id, 1, Guid.NewGuid(), "ImportDaily", "ImportDaily", BuildValueObjects());
}
