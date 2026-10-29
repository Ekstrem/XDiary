using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.Domain.Tests;

/// <summary>
/// Строит анемичные модели Record для тестов (аналог RecordDraft).
/// </summary>
public static class RecordBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string startedAt = "2026-10-29T21:00",
        int durationMin = 30,
        string partner = "Партнёр",
        string place = "Дом")
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("RecordRoot", new Dictionary<string, object?>
            {
                ["StartedAt"] = startedAt,
                ["DurationMin"] = durationMin
            }),
            ["Partner"] = new DynamicValueObject("RecordPartner", new Dictionary<string, object?>
            {
                ["Name"] = partner
            }),
            ["Place"] = new DynamicValueObject("RecordPlace", new Dictionary<string, object?>
            {
                ["Name"] = place
            }),
            ["Positions"] = new DynamicValueObject("RecordPositions", new Dictionary<string, object?>
            {
                ["Items"] = new[] { "миссионерская" }
            }),
            ["Tags"] = new DynamicValueObject("RecordTags", new Dictionary<string, object?>
            {
                ["Items"] = new[] { "романтика" }
            }),
            ["Costumes"] = new DynamicValueObject("RecordCostumes", new Dictionary<string, object?>
            {
                ["Items"] = Array.Empty<string>()
            }),
            ["Toys"] = new DynamicValueObject("RecordToys", new Dictionary<string, object?>
            {
                ["Items"] = new[] { "вибратор" }
            }),
            ["Fantasies"] = new DynamicValueObject("RecordFantasies", new Dictionary<string, object?>
            {
                ["Items"] = Array.Empty<string>()
            }),
            ["Contraception"] = new DynamicValueObject("RecordContraception", new Dictionary<string, object?>
            {
                ["Contraceptives"] = new[] { "презерватив" },
                ["ContraceptiveCount"] = 2
            }),
            ["Orgasms"] = new DynamicValueObject("RecordOrgasms", new Dictionary<string, object?>
            {
                ["OrgasmsMe"] = 1,
                ["OrgasmsPartner"] = 2,
                ["OrgasmMeType"] = new[] { "клиторальный" },
                ["OrgasmPartnerType"] = new[] { "вагинальный", "клиторальный" },
                ["OrgasmMeRating"] = 4,
                ["OrgasmPartnerRating"] = 5
            }),
            ["Origin"] = new DynamicValueObject("RecordOrigin", new Dictionary<string, object?>
            {
                ["Garmin"] = false,
                ["GarminId"] = string.Empty
            })
        };
    }

    public static IAnemicModel<IJournal> NewRecordModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "CreateRecord", "CreateRecord", BuildValueObjects());

    public static IAnemicModel<IJournal> ExistingRecordModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 1, Guid.NewGuid(), "CreateRecord", "CreateRecord", BuildValueObjects());

    public static IAnemicModel<IJournal> UpdateRecordModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "UpdateRecord", "UpdateRecord",
            BuildValueObjects(startedAt: "2026-10-29T22:00", durationMin: 45));

    public static IAnemicModel<IJournal> DeleteRecordModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "DeleteRecord", "DeleteRecord",
            new Dictionary<string, IValueObject>());

    public static IAnemicModel<IJournal> ImportRecordModel(Guid id)
        => DynamicAnemicModel<IJournal>.Create(id, 0, Guid.NewGuid(), "ImportRecord", "ImportRecord", BuildValueObjects());
}
