namespace Partners.Domain.Implementation;
using Partners.Domain.Abstraction;
// Канон EDM: VO иммутабелен — positional record (getter-only), фабрика CreateInstance.
public sealed record PartnerRoot(string Name) : IPartnerRoot
{
    public static IPartnerRoot CreateInstance(string name) => new PartnerRoot(name);
}
