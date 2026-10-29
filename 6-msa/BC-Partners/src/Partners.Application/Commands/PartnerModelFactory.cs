using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Partners.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели Partner из PartnerDraft.
/// </summary>
internal static class PartnerModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(PartnerDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("PartnerRoot", new Dictionary<string, object?>
        {
            ["Name"] = draft.Name
        }),
        ["Photo"] = new DynamicValueObject("PartnerPhoto", new Dictionary<string, object?>
        {
            ["DataUrl"] = draft.Photo
        }),
        ["Gender"] = new DynamicValueObject("PartnerGender", new Dictionary<string, object?>
        {
            ["Value"] = draft.Gender
        }),
        ["Notes"] = new DynamicValueObject("PartnerNotes", new Dictionary<string, object?>
        {
            ["Text"] = draft.Notes
        })
    };

    /// <summary>
    /// Инварианты, которые нельзя выразить DSL-метаданных (перечисления).
    /// Возвращает причину отказа или null, если всё в порядке.
    /// </summary>
    public static string? Validate(PartnerDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
            return "name is required.";
        if (draft.Gender is not ("male" or "female"))
            return "gender must be 'male' or 'female'.";
        return null;
    }
}
