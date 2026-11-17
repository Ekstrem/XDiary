namespace Partners.Domain.Implementation;
using Partners.Domain.Abstraction;
// РљР°РЅРѕРЅ EDM: VO РёРјРјСѓС‚Р°Р±РµР»РµРЅ вЂ” positional record (getter-only), С„Р°Р±СЂРёРєР° CreateInstance.
internal sealed record PartnerRoot(string Name) : IPartnerRoot
{
    public static IPartnerRoot CreateInstance(string name) => new PartnerRoot(name);
}
