using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.Domain.Tests;

/// <summary>
/// Строит анемичные модели Record для тестов (по шагам визарда).
/// </summary>
public static class RecordBuilder
{
    public static IValueObject Root(string[] partners, string startedAt) => new DynamicValueObject("RecordRoot", new Dictionary<string, object?>
    {
        ["Partners"] = partners,
        ["StartedAt"] = startedAt
    });

    public static IValueObject Origin(bool garmin, string garminId) => new DynamicValueObject("RecordOrigin", new Dictionary<string, object?>
    {
        ["Garmin"] = garmin,
        ["GarminId"] = garminId,
        ["CreatedAt"] = "2026-10-29T20:00:00",
        ["UpdatedAt"] = "2026-10-29T20:00:00"
    });

    public static Dictionary<string, IValueObject> StartVos()
        => new() { ["Root"] = Root(Array.Empty<string>(), ""), ["Origin"] = Origin(false, "") };

    public static Dictionary<string, IValueObject> WhenVos(string startedAt)
        => new() { ["Root"] = Root(Array.Empty<string>(), startedAt) };

    public static Dictionary<string, IValueObject> CompleteVos(string[] partners, string startedAt)
        => new() { ["Root"] = Root(partners, startedAt) };

    public static IAnemicModel<IJournal> NewDraftModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "StartManualEntry", "StartManualEntry", StartVos());

    public static IAnemicModel<IJournal> ExistingDraftModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 1, Guid.NewGuid(), "StartManualEntry", "StartManualEntry", StartVos());

    public static IAnemicModel<IJournal> Model(Guid id, long version, string commandName, Dictionary<string, IValueObject> vos)
        => DynamicAnemicModel<IJournal>.Create(id, version, Guid.NewGuid(), commandName, commandName, vos);
}
