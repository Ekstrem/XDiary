using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;

namespace Partners.Domain.Tests;

/// <summary>
/// Строит анемичные модели Partner для тестов (аналог PartnerDraft).
/// </summary>
public static class PartnerBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string name = "Партнёр",
        string photo = "",
        string gender = "female",
        string notes = "")
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("PartnerRoot", new Dictionary<string, object?>
            {
                ["Name"] = name
            }),
            ["Photo"] = new DynamicValueObject("PartnerPhoto", new Dictionary<string, object?>
            {
                ["DataUrl"] = photo
            }),
            ["Gender"] = new DynamicValueObject("PartnerGender", new Dictionary<string, object?>
            {
                ["Value"] = gender
            }),
            ["Notes"] = new DynamicValueObject("PartnerNotes", new Dictionary<string, object?>
            {
                ["Text"] = notes
            })
        };
    }

    public static IAnemicModel<IPartners> NewPartnerModel(Guid id)
        => DynamicAnemicModel<IPartners>.Create(id, 0, Guid.NewGuid(), "CreatePartner", "CreatePartner", BuildValueObjects());

    public static IAnemicModel<IPartners> ExistingPartnerModel(Guid id)
        => DynamicAnemicModel<IPartners>.Create(id, 1, Guid.NewGuid(), "CreatePartner", "CreatePartner", BuildValueObjects());

    public static IAnemicModel<IPartners> UpdatePartnerModel(Guid id)
        => DynamicAnemicModel<IPartners>.Create(id, 0, Guid.NewGuid(), "UpdatePartner", "UpdatePartner",
            BuildValueObjects(name: "Новый партнёр", gender: "male"));

    public static IAnemicModel<IPartners> DeletePartnerModel(Guid id)
        => DynamicAnemicModel<IPartners>.Create(id, 0, Guid.NewGuid(), "DeletePartner", "DeletePartner",
            new Dictionary<string, IValueObject>());
}
