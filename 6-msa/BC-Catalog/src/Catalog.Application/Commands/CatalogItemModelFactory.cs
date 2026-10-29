using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Catalog.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели CatalogItem из CatalogItemDraft.
/// </summary>
internal static class CatalogItemModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(CatalogItemDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("ItemRoot", new Dictionary<string, object?>
        {
            ["Kind"] = draft.Kind,
            ["Name"] = draft.Name
        }),
        ["Photo"] = new DynamicValueObject("ItemPhoto", new Dictionary<string, object?>
        {
            ["DataUrl"] = draft.Photo
        }),
        ["Notes"] = new DynamicValueObject("ItemNotes", new Dictionary<string, object?>
        {
            ["Text"] = draft.Notes
        }),
        ["Brand"] = new DynamicValueObject("ItemBrand", new Dictionary<string, object?>
        {
            ["Value"] = draft.Brand
        }),
        ["Archive"] = new DynamicValueObject("ItemArchive", new Dictionary<string, object?>
        {
            ["Archived"] = draft.Archived
        }),
        ["FantasyPartner"] = new DynamicValueObject("ItemFantasyPartner", new Dictionary<string, object?>
        {
            ["Partner"] = draft.Partner
        })
    };

    /// <summary>
    /// Инварианты, которые нельзя выразить DSL-метаданных (перечисления).
    /// </summary>
    public static string? Validate(CatalogItemDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
            return "name is required.";
        if (draft.Kind is not ("costume" or "toy" or "fantasy" or "contraception"))
            return "kind must be costume|toy|fantasy|contraception.";
        return null;
    }
}
