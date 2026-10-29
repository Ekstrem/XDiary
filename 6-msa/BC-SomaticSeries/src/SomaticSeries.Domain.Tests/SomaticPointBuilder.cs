using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;

namespace SomaticSeries.Domain.Tests;

/// <summary>
/// Строит анемичные модели SomaticPoint для тестов (аналог SomaticPointDraft).
/// </summary>
public static class SomaticPointBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string t = "2026-10-29T21:00",
        int hr = 72,
        int bodyBattery = 60)
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("SomaticPointRoot", new Dictionary<string, object?>
            {
                ["T"] = t
            }),
            ["HeartRate"] = new DynamicValueObject("HeartRate", new Dictionary<string, object?>
            {
                ["Hr"] = hr
            }),
            ["BodyBattery"] = new DynamicValueObject("BodyBattery", new Dictionary<string, object?>
            {
                ["BodyBattery"] = bodyBattery
            })
        };
    }

    public static IAnemicModel<ISomaticSeries> NewPointModel(Guid id)
        => DynamicAnemicModel<ISomaticSeries>.Create(id, 0, Guid.NewGuid(), "ImportPoint", "ImportPoint", BuildValueObjects());

    public static IAnemicModel<ISomaticSeries> ExistingPointModel(Guid id)
        => DynamicAnemicModel<ISomaticSeries>.Create(id, 1, Guid.NewGuid(), "ImportPoint", "ImportPoint", BuildValueObjects());
}
