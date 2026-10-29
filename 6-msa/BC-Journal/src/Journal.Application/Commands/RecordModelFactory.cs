using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Journal.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели Record из RecordDraft.
/// </summary>
internal static class RecordModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(RecordDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("RecordRoot", new Dictionary<string, object?>
        {
            ["StartedAt"] = draft.StartedAt,
            ["DurationMin"] = draft.DurationMin
        }),
        ["Partner"] = new DynamicValueObject("RecordPartner", new Dictionary<string, object?>
        {
            ["Name"] = draft.Partner
        }),
        ["Place"] = new DynamicValueObject("RecordPlace", new Dictionary<string, object?>
        {
            ["Name"] = draft.Place
        }),
        ["Positions"] = new DynamicValueObject("RecordPositions", new Dictionary<string, object?>
        {
            ["Items"] = draft.Positions?.ToArray() ?? Array.Empty<string>()
        }),
        ["Tags"] = new DynamicValueObject("RecordTags", new Dictionary<string, object?>
        {
            ["Items"] = draft.Tags?.ToArray() ?? Array.Empty<string>()
        }),
        ["Costumes"] = new DynamicValueObject("RecordCostumes", new Dictionary<string, object?>
        {
            ["Items"] = draft.Costumes?.ToArray() ?? Array.Empty<string>()
        }),
        ["Toys"] = new DynamicValueObject("RecordToys", new Dictionary<string, object?>
        {
            ["Items"] = draft.Toys?.ToArray() ?? Array.Empty<string>()
        }),
        ["Fantasies"] = new DynamicValueObject("RecordFantasies", new Dictionary<string, object?>
        {
            ["Items"] = draft.Fantasies?.ToArray() ?? Array.Empty<string>()
        }),
        ["Contraception"] = new DynamicValueObject("RecordContraception", new Dictionary<string, object?>
        {
            ["Contraceptives"] = draft.Contraceptives?.ToArray() ?? Array.Empty<string>(),
            ["ContraceptiveCount"] = draft.ContraceptiveCount
        }),
        ["Orgasms"] = new DynamicValueObject("RecordOrgasms", new Dictionary<string, object?>
        {
            ["OrgasmsMe"] = draft.OrgasmsMe,
            ["OrgasmsPartner"] = draft.OrgasmsPartner,
            ["OrgasmMeType"] = draft.OrgasmMeType?.ToArray() ?? Array.Empty<string>(),
            ["OrgasmPartnerType"] = draft.OrgasmPartnerType?.ToArray() ?? Array.Empty<string>(),
            ["OrgasmMeRating"] = draft.OrgasmMeRating,
            ["OrgasmPartnerRating"] = draft.OrgasmPartnerRating
        }),
        ["Origin"] = new DynamicValueObject("RecordOrigin", new Dictionary<string, object?>
        {
            ["Garmin"] = draft.Garmin,
            ["GarminId"] = draft.GarminId
        })
    };

    /// <summary>
    /// Инварианты, которые нельзя выразить DSL-метаданных (числовые диапазоны).
    /// Возвращает причину отказа или null, если всё в порядке.
    /// </summary>
    public static string? Validate(RecordDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.StartedAt))
            return "startedAt is required.";
        if (draft.DurationMin < 1)
            return "durationMin must be >= 1.";
        if (draft.OrgasmMeRating is < 0 or > 5 || draft.OrgasmPartnerRating is < 0 or > 5)
            return "orgasm rating must be in 0..5.";
        return null;
    }
}
