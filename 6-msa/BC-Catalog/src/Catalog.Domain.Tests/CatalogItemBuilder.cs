using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;

namespace Catalog.Domain.Tests;

/// <summary>
/// Строит анемичные модели CatalogItem для тестов (аналог CatalogItemDraft).
/// </summary>
public static class CatalogItemBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string kind = "toy",
        string name = "Игрушка",
        string photo = "",
        string notes = "",
        string brand = "Бренд",
        bool archived = false,
        string partner = "")
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("ItemRoot", new Dictionary<string, object?>
            {
                ["Kind"] = kind,
                ["Name"] = name
            }),
            ["Photo"] = new DynamicValueObject("ItemPhoto", new Dictionary<string, object?>
            {
                ["DataUrl"] = photo
            }),
            ["Notes"] = new DynamicValueObject("ItemNotes", new Dictionary<string, object?>
            {
                ["Text"] = notes
            }),
            ["Brand"] = new DynamicValueObject("ItemBrand", new Dictionary<string, object?>
            {
                ["Value"] = brand
            }),
            ["Archive"] = new DynamicValueObject("ItemArchive", new Dictionary<string, object?>
            {
                ["Archived"] = archived
            }),
            ["FantasyPartner"] = new DynamicValueObject("ItemFantasyPartner", new Dictionary<string, object?>
            {
                ["Partner"] = partner
            })
        };
    }

    public static IAnemicModel<ICatalog> NewCatalogItemModel(Guid id)
        => DynamicAnemicModel<ICatalog>.Create(id, 0, Guid.NewGuid(), "CreateItem", "CreateItem", BuildValueObjects());

    public static IAnemicModel<ICatalog> ExistingCatalogItemModel(Guid id)
        => DynamicAnemicModel<ICatalog>.Create(id, 1, Guid.NewGuid(), "CreateItem", "CreateItem", BuildValueObjects());

    public static IAnemicModel<ICatalog> UpdateCatalogItemModel(Guid id)
        => DynamicAnemicModel<ICatalog>.Create(id, 0, Guid.NewGuid(), "UpdateItem", "UpdateItem",
            BuildValueObjects(kind: "toy", name: "Новая игрушка", archived: true));

    public static IAnemicModel<ICatalog> DeleteCatalogItemModel(Guid id)
        => DynamicAnemicModel<ICatalog>.Create(id, 0, Guid.NewGuid(), "DeleteItem", "DeleteItem",
            new Dictionary<string, IValueObject>());
}
